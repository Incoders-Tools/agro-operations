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

    public static string? Website(string? value)
    {
        var website = Optional(value);
        return website is null || (website.Length <= 200 && !website.Any(char.IsWhiteSpace))
            ? website : throw new DomainValidationException("Website must be a single address without spaces");
    }

    /// <summary>Money amounts and quantities are stored as numeric(14,4).</summary>
    public static bool FitsAmount(decimal value) => decimal.Round(value, 4) == value && value <= 9999999999.9999m;

    // Prices and quotes are in pesos or dollars, stored upper case.
    public static string Currency(string? value)
    {
        var currency = Optional(value)?.ToUpperInvariant();
        return currency is "ARS" or "USD" ? currency : throw new DomainValidationException("Currency must be ARS or USD");
    }
}
