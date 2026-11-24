namespace AgroOperations.Api.Domain;

public sealed record LaborType(Guid Id, string Name, string? Abbreviation, long Revision);

public interface ILaborTypeRepository
{
    LaborType Create(string name, string? abbreviation);
    LaborType Update(Guid id, long revision, string name, string? abbreviation);
    IReadOnlyList<LaborType> List();
}

/// <summary>Labor is the physical operation performed (e.g. "Pulverización terrestre"); crop stage is separate.</summary>
public sealed class LaborTypeCatalog(ILaborTypeRepository repository)
{
    private static string Normalize(string name) => CatalogText.Required(name, "Name");
    private static string? Abbreviation(string? value)
    {
        var abbreviation = CatalogText.Optional(value);
        return abbreviation is not { Length: > 12 }
            ? abbreviation : throw new DomainValidationException("Abbreviation must be at most 12 characters");
    }

    public LaborType Create(string name, string? abbreviation) => repository.Create(Normalize(name), Abbreviation(abbreviation));
    public LaborType Update(Guid id, long revision, string name, string? abbreviation) =>
        repository.Update(id, revision, Normalize(name), Abbreviation(abbreviation));
    public IReadOnlyList<LaborType> List() => repository.List();
}
