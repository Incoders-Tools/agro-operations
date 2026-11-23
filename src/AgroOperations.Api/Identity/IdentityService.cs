using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Npgsql;

namespace AgroOperations.Api.Identity;

public sealed record IdentityLoginResult(Guid AccountId, string Email, string SecurityStamp);

/// <summary>
/// Invitation-only account lifecycle: invite, accept, login and password reset.
/// No HTTP endpoint is exposed yet; wiring awaits a real deployment target
/// (staging/production), per this project's own roadmap. Email delivery is
/// abstracted through IEmailSender so a verified Resend sender can replace the
/// local sink later without changing this service.
/// </summary>
public sealed class IdentityService(string connectionString, IEmailSender emailSender)
{
    private static readonly PasswordHasher<IdentityAccount> Hasher = new();
    private static readonly string DummyHash = Hasher.HashPassword(new IdentityAccount { Id = Guid.Empty, Email = "dummy" }, "Dummy-Password-0!");

    private static string Normalize(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Invalid email", nameof(email));
        try
        {
            if (!new System.Net.Mail.MailAddress(email).Address.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Invalid email", nameof(email));
        }
        catch (FormatException) { throw new ArgumentException("Invalid email", nameof(email)); }
        return email.Trim().ToUpperInvariant();
    }

    private static string Digest(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static string NewSecurityStamp() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    // ASP.NET Core Identity's default PasswordOptions policy.
    private static bool MeetsPasswordPolicy(string password) =>
        !string.IsNullOrEmpty(password) && password.Length >= 6 && password.Any(char.IsDigit) &&
        password.Any(char.IsUpper) && password.Any(char.IsLower) && password.Any(c => !char.IsLetterOrDigit(c));

    private NpgsqlConnection Open()
    {
        var db = new NpgsqlConnection(connectionString);
        db.Open();
        return db;
    }

    /// <summary>Caller must authorize the admin before calling. The token is dispatched, never returned.</summary>
    public void Invite(string email, TimeSpan lifetime)
    {
        var normalized = Normalize(email);
        if (lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromDays(7)) throw new ArgumentOutOfRangeException(nameof(lifetime));
        var token = NewToken();
        using var db = Open();
        using var cmd = new NpgsqlCommand("INSERT INTO identity_invitations(id,email,token_hash,expires_at) VALUES (@id,@email,@hash,@expires)", db);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("email", normalized);
        cmd.Parameters.AddWithValue("hash", Digest(token));
        cmd.Parameters.AddWithValue("expires", DateTime.UtcNow.Add(lifetime));
        cmd.ExecuteNonQuery();
        emailSender.SendInvitation(normalized, token);
    }

    /// <summary>Accepting with a dispatched token proves address control; verified and active both become true.</summary>
    public bool Accept(string email, string token, string password)
    {
        if (string.IsNullOrWhiteSpace(token) || !MeetsPasswordPolicy(password)) return false;
        string normalized;
        try { normalized = Normalize(email); }
        catch (ArgumentException) { return false; }
        using var db = Open();
        using var transaction = db.BeginTransaction();
        using var claim = new NpgsqlCommand("UPDATE identity_invitations SET consumed_at=now() WHERE token_hash=@hash AND email=@email AND consumed_at IS NULL AND expires_at>now() AND NOT EXISTS (SELECT 1 FROM identity_accounts WHERE email=@email) RETURNING id", db, transaction);
        claim.Parameters.AddWithValue("hash", Digest(token));
        claim.Parameters.AddWithValue("email", normalized);
        if (claim.ExecuteScalar() is null) return false;
        var account = new IdentityAccount { Id = Guid.NewGuid(), Email = normalized };
        using var insert = new NpgsqlCommand("INSERT INTO identity_accounts(id,email,password_hash,email_verified,active,security_stamp) VALUES (@id,@email,@password,true,true,@stamp) ON CONFLICT (email) DO NOTHING", db, transaction);
        insert.Parameters.AddWithValue("id", account.Id);
        insert.Parameters.AddWithValue("email", normalized);
        insert.Parameters.AddWithValue("password", Hasher.HashPassword(account, password));
        insert.Parameters.AddWithValue("stamp", NewSecurityStamp());
        if (insert.ExecuteNonQuery() != 1) return false;
        transaction.Commit();
        return true;
    }

    /// <summary>Rejects wrong password and unknown users identically; never reveals which one failed.</summary>
    public IdentityLoginResult? Login(string email, string password)
    {
        string normalized;
        try { normalized = Normalize(email); }
        catch (ArgumentException) { Hasher.VerifyHashedPassword(new IdentityAccount { Id = Guid.Empty, Email = "" }, DummyHash, password); return null; }
        using var db = Open();
        using var cmd = new NpgsqlCommand("SELECT id,email,password_hash,email_verified,active,security_stamp FROM identity_accounts WHERE email=@email", db);
        cmd.Parameters.AddWithValue("email", normalized);
        using var row = cmd.ExecuteReader();
        if (!row.Read())
        {
            Hasher.VerifyHashedPassword(new IdentityAccount { Id = Guid.Empty, Email = normalized }, DummyHash, password);
            return null;
        }
        var id = row.GetGuid(0);
        var storedEmail = row.GetString(1);
        var account = new IdentityAccount { Id = id, Email = storedEmail };
        var verification = Hasher.VerifyHashedPassword(account, row.GetString(2), password);
        var verified = row.GetBoolean(3);
        var active = row.GetBoolean(4);
        var stamp = row.GetString(5);
        return verification == PasswordVerificationResult.Failed || !verified || !active
            ? null : new IdentityLoginResult(id, storedEmail, stamp);
    }

    /// <summary>Always completes the same way for a known or unknown email; dispatch only happens for an active account.</summary>
    public void RequestPasswordReset(string email, TimeSpan lifetime)
    {
        string normalized;
        try { normalized = Normalize(email); }
        catch (ArgumentException) { return; }
        using var db = Open();
        using var find = new NpgsqlCommand("SELECT id FROM identity_accounts WHERE email=@email AND active", db);
        find.Parameters.AddWithValue("email", normalized);
        if (find.ExecuteScalar() is not Guid accountId) return;
        var token = NewToken();
        using var insert = new NpgsqlCommand("INSERT INTO identity_password_resets(id,account_id,token_hash,expires_at) VALUES (@id,@account,@hash,@expires)", db);
        insert.Parameters.AddWithValue("id", Guid.NewGuid());
        insert.Parameters.AddWithValue("account", accountId);
        insert.Parameters.AddWithValue("hash", Digest(token));
        insert.Parameters.AddWithValue("expires", DateTime.UtcNow.Add(lifetime));
        insert.ExecuteNonQuery();
        emailSender.SendPasswordReset(normalized, token);
    }

    /// <summary>Consuming a valid reset token replaces the password and bumps the stamp, invalidating prior sessions.</summary>
    public bool ResetPassword(string token, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(token) || !MeetsPasswordPolicy(newPassword)) return false;
        using var db = Open();
        using var transaction = db.BeginTransaction();
        using var claim = new NpgsqlCommand("UPDATE identity_password_resets SET consumed_at=now() WHERE token_hash=@hash AND consumed_at IS NULL AND expires_at>now() RETURNING account_id", db, transaction);
        claim.Parameters.AddWithValue("hash", Digest(token));
        if (claim.ExecuteScalar() is not Guid accountId) return false;
        var account = new IdentityAccount { Id = accountId, Email = "" };
        using var update = new NpgsqlCommand("UPDATE identity_accounts SET password_hash=@hash, security_stamp=@stamp WHERE id=@id", db, transaction);
        update.Parameters.AddWithValue("hash", Hasher.HashPassword(account, newPassword));
        update.Parameters.AddWithValue("stamp", NewSecurityStamp());
        update.Parameters.AddWithValue("id", accountId);
        update.ExecuteNonQuery();
        transaction.Commit();
        return true;
    }
}

public sealed class IdentityAccount
{
    public Guid Id { get; init; }
    public string Email { get; init; } = "";
}
