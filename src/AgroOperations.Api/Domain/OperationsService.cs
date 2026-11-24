namespace AgroOperations.Api.Domain;

public sealed record Client(Guid Id, string Name, string? TaxId, string? Producers, string? Email, string? Phone, string? Address, long Revision);
public sealed record Field(Guid Id, Guid ClientId, string Name, string? Tenure, string? Locality, long Revision);
public sealed record Plot(Guid Id, Guid FieldId, string Name, decimal AreaHectares, string? Code, long Revision);

/// <summary>Writes replace every attribute: an omitted optional attribute is stored as absent.</summary>
public sealed record ClientDraft(string Name, string? TaxId = null, string? Producers = null, string? Email = null, string? Phone = null, string? Address = null);
public sealed record FieldDraft(string Name, string? Tenure = null, string? Locality = null);
public sealed record PlotDraft(string Name, decimal AreaHectares, string? Code = null);

public sealed class DomainValidationException(string message) : Exception(message);
public sealed class DomainConflictException() : Exception("Stale revision");
public sealed class DomainNotFoundException() : Exception("Record not found");
/// <summary>A case-insensitive unique value is already taken; distinct from a stale revision.</summary>
public sealed class DomainDuplicateException(string message = "Duplicate name") : Exception(message);
/// <summary>The record exists and the revision matches, but its state forbids the change.</summary>
public sealed class DomainStateException(string message) : Exception(message);

/// <summary>Application boundary. The repository is supplied by the persistence adapter.</summary>
public sealed class OperationsService(IOperationsRepository repository)
{
    private static readonly string[] Tenures = ["propio", "alquiler_fijo", "alquiler_porcentaje"];

    private static string Name(string value) => !string.IsNullOrWhiteSpace(value)
        ? value.Trim() : throw new DomainValidationException("Name is required");

    private static ClientDraft Validated(ClientDraft draft) => new(
        Name(draft.Name), CatalogText.TaxId(draft.TaxId), CatalogText.Optional(draft.Producers),
        CatalogText.Email(draft.Email), CatalogText.Optional(draft.Phone), CatalogText.Optional(draft.Address));

    private static FieldDraft Validated(FieldDraft draft)
    {
        var name = Name(draft.Name);
        var tenure = CatalogText.Optional(draft.Tenure)?.ToLowerInvariant();
        if (tenure is not null && Array.IndexOf(Tenures, tenure) < 0)
            throw new DomainValidationException($"Tenure must be one of: {string.Join(", ", Tenures)}");
        return new(name, tenure, CatalogText.Optional(draft.Locality));
    }

    private static PlotDraft Validated(PlotDraft draft)
    {
        var name = Name(draft.Name);
        if (draft.AreaHectares <= 0 || decimal.Round(draft.AreaHectares, 4) != draft.AreaHectares || draft.AreaHectares > 99999999.9999m)
            throw new DomainValidationException("Area must be positive and fit numeric(12,4)");
        var code = CatalogText.Optional(draft.Code);
        if (code is { Length: > 40 }) throw new DomainValidationException("Code must be at most 40 characters");
        return new(name, draft.AreaHectares, code);
    }

    public Client CreateClient(ClientDraft client) => repository.CreateClient(Validated(client));
    public Field CreateField(Guid clientId, FieldDraft field) => repository.CreateField(clientId, Validated(field));
    public Plot CreatePlot(Guid fieldId, PlotDraft plot) => repository.CreatePlot(fieldId, Validated(plot));
    public Client UpdateClient(Guid id, long revision, ClientDraft client) => repository.UpdateClient(id, revision, Validated(client));
    public Field UpdateField(Guid id, long revision, FieldDraft field) => repository.UpdateField(id, revision, Validated(field));
    public Plot UpdatePlot(Guid id, long revision, PlotDraft plot) => repository.UpdatePlot(id, revision, Validated(plot));
    public Client? GetClient(Guid id) => repository.GetClient(id);
    public Field? GetField(Guid id) => repository.GetField(id);
    public Plot? GetPlot(Guid id) => repository.GetPlot(id);
    public IReadOnlyList<Client> ListClients() => repository.ListClients();
    /// <summary>Without a client, every field.</summary>
    public IReadOnlyList<Field> ListFields(Guid? clientId = null) => repository.ListFields(clientId);
    /// <summary>Without a field, every plot.</summary>
    public IReadOnlyList<Plot> ListPlots(Guid? fieldId = null) => repository.ListPlots(fieldId);
}

public interface IOperationsRepository
{
    Client CreateClient(ClientDraft client);
    Field CreateField(Guid clientId, FieldDraft field);
    Plot CreatePlot(Guid fieldId, PlotDraft plot);
    Client UpdateClient(Guid id, long revision, ClientDraft client);
    Field UpdateField(Guid id, long revision, FieldDraft field);
    Plot UpdatePlot(Guid id, long revision, PlotDraft plot);
    Client? GetClient(Guid id);
    Field? GetField(Guid id);
    Plot? GetPlot(Guid id);
    IReadOnlyList<Client> ListClients();
    IReadOnlyList<Field> ListFields(Guid? clientId);
    IReadOnlyList<Plot> ListPlots(Guid? fieldId);
}
