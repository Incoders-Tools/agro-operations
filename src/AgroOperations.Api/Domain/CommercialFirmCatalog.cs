namespace AgroOperations.Api.Domain;

public sealed record CommercialFirm(Guid Id, Guid ClientId, string Name, string? TaxId, int NextWorkOrderNumber, long Revision);

public interface ICommercialFirmRepository
{
    CommercialFirm Create(Guid clientId, string name, string? taxId);
    /// <summary>A null next work order number keeps the stored one.</summary>
    CommercialFirm Update(Guid id, long revision, string name, string? taxId, int? nextWorkOrderNumber);
    IReadOnlyList<CommercialFirm> List(Guid? clientId);
}

/// <summary>A client works through one or more commercial firms; a firm never moves to another client.</summary>
public sealed class CommercialFirmCatalog(ICommercialFirmRepository repository)
{
    public CommercialFirm Create(Guid clientId, string name, string? taxId) =>
        repository.Create(clientId, CatalogText.Required(name, "Name"), CatalogText.TaxId(taxId));
    /// <summary>
    /// The next work order number continues the firm's numbering; it must exceed every issued number,
    /// which the repository checks against the stored orders. Without one, the stored number is kept.
    /// </summary>
    public CommercialFirm Update(Guid id, long revision, string name, string? taxId, int? nextWorkOrderNumber = null) =>
        repository.Update(id, revision, CatalogText.Required(name, "Name"), CatalogText.TaxId(taxId), nextWorkOrderNumber);
    /// <summary>Without a client, every firm.</summary>
    public IReadOnlyList<CommercialFirm> List(Guid? clientId = null) => repository.List(clientId);
}
