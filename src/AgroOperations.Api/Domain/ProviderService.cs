namespace AgroOperations.Api.Domain;

public sealed record Provider(Guid Id, string Name, string Kind, string? TaxId, string? Website, string? Phone, string? Email, string? Notes, long Revision);
public sealed record ProviderDraft(string Name, string? Kind, string? TaxId, string? Website, string? Phone, string? Email, string? Notes);

public sealed record ProviderContact(Guid Id, Guid ProviderId, string Name, string Role, string? Phone, string? Email, string? Notes, long Revision);
/// <summary>A contact keeps its provider: the draft has none.</summary>
public sealed record ProviderContactDraft(string Name, string? Role, string? Phone, string? Email, string? Notes);

public sealed record ClientProvider(Guid Id, Guid ClientId, Guid ProviderId);

public sealed record ProviderPrice(Guid Id, Guid ProviderId, Guid InputId, decimal Price, string Currency, string Unit, DateOnly ValidOn, string? Notes,
    long Revision);
/// <summary>
/// Create requires the provider and the input; on update an absent one keeps the current one. An absent unit is the input's unit.
/// </summary>
public sealed record ProviderPriceDraft(Guid? ProviderId, Guid? InputId, decimal Price, string? Currency, string? Unit, DateOnly? ValidOn, string? Notes);

public interface IProviderRepository
{
    Provider Create(ProviderDraft provider);
    Provider Update(Guid id, long revision, ProviderDraft provider);
    IReadOnlyList<Provider> List(string? kind);
    ProviderContact CreateContact(Guid providerId, ProviderContactDraft contact);
    ProviderContact UpdateContact(Guid id, long revision, ProviderContactDraft contact);
    IReadOnlyList<ProviderContact> ListContacts(Guid? providerId);
    ClientProvider Link(Guid clientId, Guid providerId);
    /// <summary>A missing link is not found.</summary>
    void Unlink(Guid id);
    IReadOnlyList<ClientProvider> ListLinks(Guid? clientId, Guid? providerId);
    ProviderPrice CreatePrice(ProviderPriceDraft price);
    ProviderPrice UpdatePrice(Guid id, long revision, ProviderPriceDraft price);
    IReadOnlyList<ProviderPrice> ListPrices(Guid? providerId, Guid? inputId);
}

/// <summary>
/// A provider is a commercial firm clients buy inputs from, or a service provider such as a soil or seed laboratory or a
/// virtual platform. Contractors stay a separate catalog because work orders reference them.
/// </summary>
public sealed class ProviderService(IProviderRepository repository)
{
    private static readonly string[] Kinds = ["insumos", "laboratorio_suelos", "laboratorio_semillas", "plataforma_virtual", "otro"];
    private static readonly string[] Roles = ["ventas", "tecnico", "otro"];

    private static string OneOf(string? value, string[] allowed, string field)
    {
        var normalized = CatalogText.Optional(value)?.ToLowerInvariant();
        return normalized is not null && Array.IndexOf(allowed, normalized) >= 0
            ? normalized : throw new DomainValidationException($"{field} must be one of: {string.Join(", ", allowed)}");
    }

    private static ProviderDraft Validated(ProviderDraft draft) => new(
        CatalogText.Required(draft.Name, "Name"), OneOf(draft.Kind, Kinds, "Kind"), CatalogText.TaxId(draft.TaxId), CatalogText.Website(draft.Website),
        CatalogText.Optional(draft.Phone), CatalogText.Email(draft.Email), CatalogText.Optional(draft.Notes));

    private static ProviderContactDraft Validated(ProviderContactDraft draft) => new(
        CatalogText.Required(draft.Name, "Name"), OneOf(draft.Role, Roles, "Role"), CatalogText.Optional(draft.Phone),
        CatalogText.Email(draft.Email), CatalogText.Optional(draft.Notes));

    private static ProviderPriceDraft Validated(ProviderPriceDraft draft)
    {
        if (draft.Price <= 0 || !CatalogText.FitsAmount(draft.Price)) throw new DomainValidationException("Price must be positive and fit numeric(14,4)");
        var currency = CatalogText.Currency(draft.Currency);
        if (draft.ValidOn is null) throw new DomainValidationException("Valid on is required");
        return draft with { Currency = currency, Unit = CatalogText.Optional(draft.Unit), Notes = CatalogText.Optional(draft.Notes) };
    }

    public Provider Create(ProviderDraft provider) => repository.Create(Validated(provider));
    public Provider Update(Guid id, long revision, ProviderDraft provider) => repository.Update(id, revision, Validated(provider));
    /// <summary>Without a kind, every provider; an unknown kind is rejected rather than matching nothing.</summary>
    public IReadOnlyList<Provider> List(string? kind = null) =>
        repository.List(CatalogText.Optional(kind) is null ? null : OneOf(kind, Kinds, "Kind"));

    public ProviderContact CreateContact(Guid providerId, ProviderContactDraft contact) => repository.CreateContact(providerId, Validated(contact));
    public ProviderContact UpdateContact(Guid id, long revision, ProviderContactDraft contact) => repository.UpdateContact(id, revision, Validated(contact));
    /// <summary>Without a provider, every contact.</summary>
    public IReadOnlyList<ProviderContact> ListContacts(Guid? providerId = null) => repository.ListContacts(providerId);

    /// <summary>A client buys from a provider; a pair is linked once.</summary>
    public ClientProvider Link(Guid clientId, Guid providerId) => repository.Link(clientId, providerId);
    public void Unlink(Guid id) => repository.Unlink(id);
    /// <summary>Both filters are optional and combine.</summary>
    public IReadOnlyList<ClientProvider> ListLinks(Guid? clientId = null, Guid? providerId = null) => repository.ListLinks(clientId, providerId);

    public ProviderPrice CreatePrice(ProviderPriceDraft price)
    {
        if (price.ProviderId is null) throw new DomainValidationException("Provider is required");
        if (price.InputId is null) throw new DomainValidationException("Input is required");
        return repository.CreatePrice(Validated(price));
    }
    public ProviderPrice UpdatePrice(Guid id, long revision, ProviderPriceDraft price) => repository.UpdatePrice(id, revision, Validated(price));
    /// <summary>Newest valid date first; both filters are optional and combine.</summary>
    public IReadOnlyList<ProviderPrice> ListPrices(Guid? providerId = null, Guid? inputId = null) => repository.ListPrices(providerId, inputId);
}
