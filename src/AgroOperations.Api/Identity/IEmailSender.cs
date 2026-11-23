namespace AgroOperations.Api.Identity;

/// <summary>Abstracts outbound mail so a verified Resend sender can replace the local sink later.</summary>
public interface IEmailSender
{
    void SendInvitation(string email, string token);
    void SendPasswordReset(string email, string token);
}

/// <summary>Non-production sink: writes each message to a local, git-ignored file for manual delivery.</summary>
public sealed class LocalFileEmailSender(string outboxPath) : IEmailSender
{
    private void Append(string subject, string email, string token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outboxPath)!);
        File.AppendAllText(outboxPath,
            $"[{DateTime.UtcNow:O}] {subject} -> {email}{Environment.NewLine}token: {token}{Environment.NewLine}{Environment.NewLine}");
    }

    public void SendInvitation(string email, string token) => Append("Invitación", email, token);
    public void SendPasswordReset(string email, string token) => Append("Restablecer contraseña", email, token);
}
