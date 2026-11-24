namespace AgroOperations.Api.Domain;

public sealed record Contractor(Guid Id, string Name, string? TaxId, string? Phone, string? Email, string? Notes, bool OwnEquipment, long Revision);
public sealed record ContractorDraft(string Name, string? TaxId, string? Phone, string? Email, string? Notes, bool OwnEquipment);

public interface IContractorRepository
{
    Contractor Create(ContractorDraft contractor);
    Contractor Update(Guid id, long revision, ContractorDraft contractor);
    IReadOnlyList<Contractor> List();
}

/// <summary>A contractor performs labors on a work order; "own equipment" marks the client's own machinery.</summary>
public sealed class ContractorCatalog(IContractorRepository repository)
{
    private static ContractorDraft Validated(ContractorDraft draft) => new(
        CatalogText.Required(draft.Name, "Name"), CatalogText.TaxId(draft.TaxId), CatalogText.Optional(draft.Phone),
        CatalogText.Email(draft.Email), CatalogText.Optional(draft.Notes), draft.OwnEquipment);

    public Contractor Create(ContractorDraft contractor) => repository.Create(Validated(contractor));
    public Contractor Update(Guid id, long revision, ContractorDraft contractor) => repository.Update(id, revision, Validated(contractor));
    public IReadOnlyList<Contractor> List() => repository.List();
}
