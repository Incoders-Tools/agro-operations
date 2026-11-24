namespace AgroOperations.Api.Domain;

/// <summary>Shared text rules for catalog fields: required values are trimmed, blank optional values are absent.</summary>
internal static class CatalogText
{
    public static string Required(string? value, string field) => !string.IsNullOrWhiteSpace(value)
        ? value.Trim() : throw new DomainValidationException($"{field} is required");

    public static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
