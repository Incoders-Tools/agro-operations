namespace AgroOperations.Api.Domain;

public sealed record LaborType(Guid Id, string Name, string? Abbreviation, string? Category, string? Description, long Revision);

public interface ILaborTypeRepository
{
    LaborType Create(string name, string? abbreviation, string? category, string? description);
    LaborType Update(Guid id, long revision, string name, string? abbreviation, string? category, string? description);
    IReadOnlyList<LaborType> List();
}

/// <summary>
/// Labor is the physical operation performed (e.g. "Pulverización terrestre"); crop stage is separate.
/// Category is the domain reference's grouping and description its explanatory note; writes replace both.
/// </summary>
public sealed class LaborTypeCatalog(ILaborTypeRepository repository)
{
    private static readonly string[] Categories = ["aplicacion", "labranza", "siembra", "cosecha", "forrajes", "infraestructura"];

    private static string Normalize(string name) => CatalogText.Required(name, "Name");
    private static string? Abbreviation(string? value)
    {
        var abbreviation = CatalogText.Optional(value);
        return abbreviation is not { Length: > 12 }
            ? abbreviation : throw new DomainValidationException("Abbreviation must be at most 12 characters");
    }
    private static string? Category(string? value)
    {
        var category = CatalogText.Optional(value)?.ToLowerInvariant();
        return category is null || Array.IndexOf(Categories, category) >= 0
            ? category : throw new DomainValidationException($"Category must be one of: {string.Join(", ", Categories)}");
    }
    private static string? Description(string? value)
    {
        var description = CatalogText.Optional(value);
        return description is not { Length: > 300 }
            ? description : throw new DomainValidationException("Description must be at most 300 characters");
    }

    public LaborType Create(string name, string? abbreviation, string? category = null, string? description = null) =>
        repository.Create(Normalize(name), Abbreviation(abbreviation), Category(category), Description(description));
    public LaborType Update(Guid id, long revision, string name, string? abbreviation, string? category = null, string? description = null) =>
        repository.Update(id, revision, Normalize(name), Abbreviation(abbreviation), Category(category), Description(description));
    public IReadOnlyList<LaborType> List() => repository.List();
}
