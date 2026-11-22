namespace AgroOperations.Api.Domain;

public sealed record Client(Guid Id, string Name, long Revision);
public sealed record Field(Guid Id, Guid ClientId, string Name, long Revision);
public sealed record Plot(Guid Id, Guid FieldId, string Name, decimal AreaHectares, long Revision);

public sealed class DomainValidationException(string message) : Exception(message);
public sealed class DomainConflictException() : Exception("Stale revision");
public sealed class DomainNotFoundException() : Exception("Record not found");

/// <summary>Application boundary. The repository is supplied by the persistence adapter.</summary>
public sealed class OperationsService(IOperationsRepository repository)
{
    private static string Name(string value) => !string.IsNullOrWhiteSpace(value)
        ? value.Trim() : throw new DomainValidationException("Name is required");

    public Client CreateClient(string name) => repository.CreateClient(Name(name));
    public Field CreateField(Guid clientId, string name) => repository.CreateField(clientId, Name(name));
    public Plot CreatePlot(Guid fieldId, string name, decimal areaHectares)
    {
        var normalized = Name(name);
        if (areaHectares <= 0 || decimal.Round(areaHectares, 4) != areaHectares || areaHectares > 99999999.9999m)
            throw new DomainValidationException("Area must be positive and fit numeric(12,4)");
        return repository.CreatePlot(fieldId, normalized, areaHectares);
    }
    public Client UpdateClient(Guid id, long revision, string name) => repository.UpdateClient(id, revision, Name(name));
    public Field UpdateField(Guid id, long revision, string name) => repository.UpdateField(id, revision, Name(name));
    public Plot UpdatePlot(Guid id, long revision, string name, decimal areaHectares)
    {
        var normalized = Name(name);
        if (areaHectares <= 0 || decimal.Round(areaHectares, 4) != areaHectares || areaHectares > 99999999.9999m)
            throw new DomainValidationException("Area must be positive and fit numeric(12,4)");
        return repository.UpdatePlot(id, revision, normalized, areaHectares);
    }
    public Client? GetClient(Guid id) => repository.GetClient(id);
    public Field? GetField(Guid id) => repository.GetField(id);
    public Plot? GetPlot(Guid id) => repository.GetPlot(id);
    public IReadOnlyList<Client> ListClients() => repository.ListClients();
    public IReadOnlyList<Field> ListFields() => repository.ListFields();
    public IReadOnlyList<Plot> ListPlots() => repository.ListPlots();
}

public interface IOperationsRepository
{
    Client CreateClient(string name);
    Field CreateField(Guid clientId, string name);
    Plot CreatePlot(Guid fieldId, string name, decimal areaHectares);
    Client UpdateClient(Guid id, long revision, string name);
    Field UpdateField(Guid id, long revision, string name);
    Plot UpdatePlot(Guid id, long revision, string name, decimal areaHectares);
    Client? GetClient(Guid id);
    Field? GetField(Guid id);
    Plot? GetPlot(Guid id);
    IReadOnlyList<Client> ListClients();
    IReadOnlyList<Field> ListFields();
    IReadOnlyList<Plot> ListPlots();
}
