namespace AgroOperations.Api.Domain;

public sealed record Stage(Guid Id, string Name, int SortOrder, long Revision);

public interface IStageRepository
{
    Stage Create(string name, int sortOrder);
    Stage Update(Guid id, long revision, string name, int sortOrder);
    IReadOnlyList<Stage> List();
}

/// <summary>Stage is when a labor happens in the crop cycle (e.g. "Presiembra"), listed by sort order.</summary>
public sealed class StageCatalog(IStageRepository repository)
{
    public Stage Create(string name, int sortOrder) => repository.Create(CatalogText.Required(name, "Name"), sortOrder);
    public Stage Update(Guid id, long revision, string name, int sortOrder) =>
        repository.Update(id, revision, CatalogText.Required(name, "Name"), sortOrder);
    public IReadOnlyList<Stage> List() => repository.List();
}
