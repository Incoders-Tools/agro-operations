namespace AgroOperations.Api.Domain;

public sealed record Campaign(Guid Id, string Name, long Revision);

public interface ICampaignRepository
{
    Campaign Create(string name);
    Campaign Update(Guid id, long revision, string name);
    IReadOnlyList<Campaign> List();
}

/// <summary>A campaign is a shared agricultural season label (e.g. "2026-27"), not tied to one client.</summary>
public sealed class CampaignCatalog(ICampaignRepository repository)
{
    private static string Normalize(string name) => !string.IsNullOrWhiteSpace(name)
        ? name.Trim() : throw new DomainValidationException("Name is required");

    public Campaign Create(string name) => repository.Create(Normalize(name));
    public Campaign Update(Guid id, long revision, string name) => repository.Update(id, revision, Normalize(name));
    public IReadOnlyList<Campaign> List() => repository.List();
}
