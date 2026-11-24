namespace AgroOperations.Api.Domain;

/// <summary>Shared text rules for catalog fields: required values are trimmed, blank optional values are absent.</summary>
internal static class CatalogText
{
    public static string Required(string? value, string field) => !string.IsNullOrWhiteSpace(value)
        ? value.Trim() : throw new DomainValidationException($"{field} is required");

    public static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // The CUIT is stored as its 11 digits; hyphens and spaces are the only accepted separators.
    public static string? TaxId(string? value)
    {
        if (Optional(value) is not { } taxId) return null;
        var digits = taxId.Replace("-", "").Replace(" ", "");
        return digits.Length == 11 && digits.All(char.IsAsciiDigit)
            ? digits : throw new DomainValidationException("CUIT must have 11 digits");
    }

    public static string? Email(string? value)
    {
        if (Optional(value) is not { } email) return null;
        var at = email.IndexOf('@');
        return at > 0 && at < email.Length - 1 && email.IndexOf('@', at + 1) < 0
            ? email : throw new DomainValidationException("Email must contain a single @ with text on both sides");
    }
}
