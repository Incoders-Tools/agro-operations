using AgroOperations.Api.Identity;
using Npgsql;

var source = Environment.GetEnvironmentVariable("AGRO_TEST_DATABASE");
if (string.IsNullOrWhiteSpace(source))
{
    Console.Error.WriteLine("AGRO_TEST_DATABASE is required for isolated PostgreSQL identity tests");
    return 2;
}
var schema = "identity_test_" + Guid.NewGuid().ToString("N");
var cs = new NpgsqlConnectionStringBuilder(source) { SearchPath = schema }.ConnectionString;
using var admin = new NpgsqlConnection(source);
admin.Open();
using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin)) create.ExecuteNonQuery();
try
{
    using (var db = new NpgsqlConnection(cs))
    {
        db.Open();
        var sql = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/AgroOperations.Api/Persistence/Migrations/002_identity.sql"));
        using var migrate = new NpgsqlCommand(File.ReadAllText(sql), db);
        migrate.ExecuteNonQuery();
    }
    var sender = new CapturingEmailSender();
    var service = new IdentityService(cs, sender);
    const string email = "invited@example.test";
    const string password = "Safe-Password-42!";
    const string otherPassword = "Other-Password-77!";
    static void Check(bool ok, string reason) { if (!ok) throw new Exception(reason); }
    long Count(string table)
    {
        using var db = new NpgsqlConnection(cs);
        db.Open();
        using var cmd = new NpgsqlCommand($"SELECT count(*) FROM {table}", db);
        return (long)cmd.ExecuteScalar()!;
    }

    // S1/S2: invitation-only creation, one-use expiring non-plaintext token, no account until acceptance.
    Check(Count("identity_accounts") == 0, "default account");
    service.Invite(email, TimeSpan.FromHours(1));
    Check(Count("identity_accounts") == 0 && Count("identity_invitations") == 1, "invite created account");
    var invitation = sender.LastInvitation(email) ?? throw new Exception("invitation not dispatched");
    using (var db = new NpgsqlConnection(cs))
    {
        db.Open();
        using var cmd = new NpgsqlCommand("SELECT token_hash FROM identity_invitations", db);
        Check((string)cmd.ExecuteScalar()! != invitation, "plaintext token stored");
    }
    Check(!service.Accept(email, "invalid", password), "invalid token");
    Check(!service.Accept("other@example.test", invitation, password), "mismatched email");
    Check(!service.Accept(email, invitation, "weak"), "weak password");
    Check(Count("identity_accounts") == 0 && Count("identity_invitations") == 1, "rejected input mutated account or invitation");
    Check(service.Accept(email, invitation, password), "valid invitation");
    Check(Count("identity_accounts") == 1 && Count("identity_invitations") == 1, "acceptance count");
    string firstStamp;
    using (var db = new NpgsqlConnection(cs))
    {
        db.Open();
        using var cmd = new NpgsqlCommand("SELECT email_verified,active,security_stamp FROM identity_accounts WHERE email=@email", db);
        cmd.Parameters.AddWithValue("email", email.ToUpperInvariant());
        using var row = cmd.ExecuteReader();
        Check(row.Read() && row.GetBoolean(0) && row.GetBoolean(1), "accepted account not verified/active");
        firstStamp = row.GetString(2);
        Check(!string.IsNullOrWhiteSpace(firstStamp), "missing security stamp");
    }
    Check(!service.Accept(email, invitation, password) && Count("identity_accounts") == 1, "token replay");
    service.Invite("expired@example.test", TimeSpan.FromMilliseconds(1));
    var expiredToken = sender.LastInvitation("expired@example.test")!;
    await Task.Delay(50);
    Check(!service.Accept("expired@example.test", expiredToken, password) && Count("identity_accounts") == 1, "expired invitation accepted");
    try { service.Invite("bad@example.test", TimeSpan.Zero); throw new Exception("zero lifetime accepted"); } catch (ArgumentOutOfRangeException) { }

    // S3: login succeeds only for a verified active user; wrong password/unknown user fail identically.
    Check(service.Login("not-an-email", password) is null, "malformed email logged in");
    Check(service.Login("unknown@example.test", password) is null, "unknown user logged in");
    Check(service.Login(email, "Wrong-Password-1!") is null, "wrong password accepted");
    var login = service.Login(email, password) ?? throw new Exception("valid login rejected");
    Check(login.Email == email.ToUpperInvariant() && login.SecurityStamp == firstStamp, "login result mismatch");

    // S4: password-reset is a no-op for unknown email (no dispatch, no stored row), dispatches only for a known active account,
    // and resetting invalidates the previous password/stamp while rejecting replay and expiry without mutating state.
    service.RequestPasswordReset("not-an-email", TimeSpan.FromHours(1));
    Check(Count("identity_password_resets") == 0, "reset stored for malformed email");
    service.RequestPasswordReset("unknown@example.test", TimeSpan.FromHours(1));
    Check(sender.LastReset("unknown@example.test") is null && Count("identity_password_resets") == 0, "reset dispatched for unknown email");
    service.RequestPasswordReset(email, TimeSpan.FromHours(1));
    var resetToken = sender.LastReset(email) ?? throw new Exception("reset not dispatched");
    Check(Count("identity_password_resets") == 1, "reset token not stored");
    Check(!service.ResetPassword("invalid", otherPassword) && Count("identity_password_resets") == 1, "invalid reset token accepted");
    Check(!service.ResetPassword(resetToken, "weak") && service.Login(email, password) is not null, "weak reset password changed credentials");
    Check(service.ResetPassword(resetToken, otherPassword), "valid reset token rejected");
    Check(service.Login(email, password) is null, "old password still valid after reset");
    var loginAfterReset = service.Login(email, otherPassword) ?? throw new Exception("new password rejected after reset");
    Check(loginAfterReset.SecurityStamp != firstStamp, "security stamp unchanged after reset");
    Check(!service.ResetPassword(resetToken, "Another-Password-9!"), "reset token replay accepted");
    service.RequestPasswordReset(email, TimeSpan.FromMilliseconds(1));
    var secondResetToken = sender.LastReset(email)!;
    await Task.Delay(50);
    Check(!service.ResetPassword(secondResetToken, "Yet-Another-9!") && service.Login(email, otherPassword) is not null,
        "expired reset token changed credentials");

    Console.WriteLine("PASS: isolated identity lifecycle (invite, accept, login, password reset) and rejected-input invariants");
    return 0;
}
finally
{
    using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin);
    drop.ExecuteNonQuery();
}

internal sealed class CapturingEmailSender : IEmailSender
{
    private readonly Dictionary<string, string> invitations = new();
    private readonly Dictionary<string, string> resets = new();
    public void SendInvitation(string email, string token) => invitations[email] = token;
    public void SendPasswordReset(string email, string token) => resets[email] = token;
    public string? LastInvitation(string email) => invitations.GetValueOrDefault(email.Trim().ToUpperInvariant());
    public string? LastReset(string email) => resets.GetValueOrDefault(email.Trim().ToUpperInvariant());
}
