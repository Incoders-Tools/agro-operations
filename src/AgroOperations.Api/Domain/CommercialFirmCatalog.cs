namespace AgroOperations.Api.Domain;

public sealed record CommercialFirm(Guid Id, Guid ClientId, string Name, string? TaxId, long Revision);

public interface ICommercialFirmRepository
{
    CommercialFirm Create(Guid clientId, string name, string? taxId);
    CommercialFirm Update(Guid id, long revision, string name, string? taxId);
    IReadOnlyList<CommercialFirm> List(Guid? clientId);
}

/// <summary>A client works through one or more commercial firms; a firm never moves to another client.</summary>
public sealed class CommercialFirmCatalog(ICommercialFirmRepository repository)
{
    public CommercialFirm Create(Guid clientId, string name, string? taxId) =>
        repository.Create(clientId, CatalogText.Required(name, "Name"), CatalogText.TaxId(taxId));
    public CommercialFirm Update(Guid id, long revision, string name, string? taxId) =>
        repository.Update(id, revision, CatalogText.Required(name, "Name"), CatalogText.TaxId(taxId));
    /// <summary>Without a client, every firm.</summary>
    public IReadOnlyList<CommercialFirm> List(Guid? clientId = null) => repository.List(clientId);
}
