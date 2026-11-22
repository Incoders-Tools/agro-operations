namespace AgroOperations.Api.Domain;

public sealed record Crop(Guid Id, string Name, long Revision);

public interface ICropRepository
{
    Crop Create(string name);
    Crop Update(Guid id, long revision, string name);
    IReadOnlyList<Crop> List();
}

/// <summary>Catalog names are human-reviewed; source crop sequences are not split automatically.</summary>
public sealed class CropCatalog(ICropRepository repository)
{
    private static string Normalize(string name) => !string.IsNullOrWhiteSpace(name)
        ? name.Trim() : throw new DomainValidationException("Name is required");

    public Crop Create(string name) => repository.Create(Normalize(name));
    public Crop Update(Guid id, long revision, string name) => repository.Update(id, revision, Normalize(name));
    public IReadOnlyList<Crop> List() => repository.List();
}
