namespace AgroOperations.Api.Domain;

public sealed record LaborType(Guid Id, string Name, long Revision);

public interface ILaborTypeRepository
{
    LaborType Create(string name);
    LaborType Update(Guid id, long revision, string name);
    IReadOnlyList<LaborType> List();
}

/// <summary>Labor is the physical operation performed (e.g. "Pulverización terrestre"); crop stage is separate.</summary>
public sealed class LaborTypeCatalog(ILaborTypeRepository repository)
{
    private static string Normalize(string name) => !string.IsNullOrWhiteSpace(name)
        ? name.Trim() : throw new DomainValidationException("Name is required");

    public LaborType Create(string name) => repository.Create(Normalize(name));
    public LaborType Update(Guid id, long revision, string name) => repository.Update(id, revision, Normalize(name));
    public IReadOnlyList<LaborType> List() => repository.List();
}
