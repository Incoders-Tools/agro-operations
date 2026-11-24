using AgroOperations.Api.Domain;
using Npgsql;

namespace AgroOperations.Api.Persistence;

/// <summary>One statement per mutation; revision predicates make updates atomic across processes.</summary>
public sealed class PostgresOperationsRepository(string connectionString) : IOperationsRepository
{
    private const string ClientColumns = "id,name,tax_id,producers,email,phone,address,revision";
    private const string FieldColumns = "id,client_id,name,tenure,locality,revision";
    private const string PlotColumns = "id,field_id,name,area_hectares,code,revision";

    private T Execute<T>(string sql, Action<NpgsqlCommand> parameters, Func<NpgsqlDataReader, T> map)
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        parameters(command);
        try
        {
            using var reader = command.ExecuteReader();
            if (!reader.Read()) throw new DomainNotFoundException();
            return map(reader);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // plots.code is the only unique value of the hierarchy.
            throw new DomainDuplicateException("Duplicate code");
        }
    }

    private static void Add(NpgsqlCommand command, string key, object? value) => command.Parameters.AddWithValue(key, value ?? DBNull.Value);
    private static string? Text(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
    private static Client ClientRow(NpgsqlDataReader r) =>
        new(r.GetGuid(0), r.GetString(1), Text(r, 2), Text(r, 3), Text(r, 4), Text(r, 5), Text(r, 6), r.GetInt64(7));
    private static Field FieldRow(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), Text(r, 3), Text(r, 4), r.GetInt64(5));
    private static Plot PlotRow(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetDecimal(3), Text(r, 4), r.GetInt64(5));

    private static void Bind(NpgsqlCommand c, ClientDraft client)
    {
        Add(c, "name", client.Name); Add(c, "taxId", client.TaxId); Add(c, "producers", client.Producers);
        Add(c, "email", client.Email); Add(c, "phone", client.Phone); Add(c, "address", client.Address);
    }
    private static void Bind(NpgsqlCommand c, FieldDraft field)
    {
        Add(c, "name", field.Name); Add(c, "tenure", field.Tenure); Add(c, "locality", field.Locality);
    }
    private static void Bind(NpgsqlCommand c, PlotDraft plot)
    {
        Add(c, "name", plot.Name); Add(c, "area", plot.AreaHectares); Add(c, "code", plot.Code);
    }

    // One statement also creates the client's commercial firm with the client's name. Firm names are unique across
    // clients, so when the name is already a firm's the client is still created, without a firm: the name a client is
    // known by must not block registering it, and a firm with another name can be added later.
    public Client CreateClient(ClientDraft client) => Execute($"""
        WITH client AS (
          INSERT INTO clients(id,name,tax_id,producers,email,phone,address) VALUES (@id,@name,@taxId,@producers,@email,@phone,@address)
          RETURNING {ClientColumns}),
        firm AS (
          INSERT INTO commercial_firms(id,client_id,name) SELECT @firmId,id,name FROM client ON CONFLICT ((lower(name))) DO NOTHING)
        SELECT {ClientColumns} FROM client
        """,
        c => { Add(c, "id", Guid.NewGuid()); Add(c, "firmId", Guid.NewGuid()); Bind(c, client); }, ClientRow);

    public Field CreateField(Guid clientId, FieldDraft field) => Execute(
        $"INSERT INTO fields(id,client_id,name,tenure,locality) SELECT @id,id,@name,@tenure,@locality FROM clients WHERE id=@parent RETURNING {FieldColumns}",
        c => { Add(c, "id", Guid.NewGuid()); Add(c, "parent", clientId); Bind(c, field); }, FieldRow);

    public Plot CreatePlot(Guid fieldId, PlotDraft plot) => Execute(
        $"INSERT INTO plots(id,field_id,name,area_hectares,code) SELECT @id,id,@name,@area,@code FROM fields WHERE id=@parent RETURNING {PlotColumns}",
        c => { Add(c, "id", Guid.NewGuid()); Add(c, "parent", fieldId); Bind(c, plot); }, PlotRow);

    private T Update<T>(string sql, Guid id, long revision, Action<NpgsqlCommand> extra, Func<NpgsqlDataReader, T> map, Func<Guid, bool> exists)
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        Add(command, "id", id);
        Add(command, "expected", revision);
        extra(command);
        try
        {
            using (var reader = command.ExecuteReader())
                if (reader.Read()) return map(reader);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DomainDuplicateException("Duplicate code");
        }
        if (exists(id)) throw new DomainConflictException();
        throw new DomainNotFoundException();
    }

    public Client UpdateClient(Guid id, long revision, ClientDraft client) => Update(
        $"UPDATE clients SET name=@name,tax_id=@taxId,producers=@producers,email=@email,phone=@phone,address=@address,revision=revision+1 WHERE id=@id AND revision=@expected RETURNING {ClientColumns}",
        id, revision, c => Bind(c, client), ClientRow, key => GetClient(key) is not null);
    // A field or plot moves only to an existing parent; an absent parent keeps the current one. A missing record or
    // parent is not found; only an existing record with an existing parent is stale. Work orders keep their plots.
    public Field UpdateField(Guid id, long revision, FieldDraft field) => Update($"""
        UPDATE fields SET name=@name,tenure=@tenure,locality=@locality,client_id=coalesce(@parent::uuid,client_id),revision=revision+1
        WHERE id=@id AND revision=@expected AND (@parent::uuid IS NULL OR EXISTS(SELECT 1 FROM clients WHERE id=@parent::uuid))
        RETURNING {FieldColumns}
        """,
        id, revision, c => { Bind(c, field); Add(c, "parent", field.ClientId); }, FieldRow,
        key => GetField(key) is not null && (field.ClientId is not { } parent || GetClient(parent) is not null));
    public Plot UpdatePlot(Guid id, long revision, PlotDraft plot) => Update($"""
        UPDATE plots SET name=@name,area_hectares=@area,code=@code,field_id=coalesce(@parent::uuid,field_id),revision=revision+1
        WHERE id=@id AND revision=@expected AND (@parent::uuid IS NULL OR EXISTS(SELECT 1 FROM fields WHERE id=@parent::uuid))
        RETURNING {PlotColumns}
        """,
        id, revision, c => { Bind(c, plot); Add(c, "parent", plot.FieldId); }, PlotRow,
        key => GetPlot(key) is not null && (plot.FieldId is not { } parent || GetField(parent) is not null));

    private T? Find<T>(string sql, Guid id, Func<NpgsqlDataReader, T> map) where T : class
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        Add(command, "id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? map(reader) : null;
    }
    public Client? GetClient(Guid id) => Find($"SELECT {ClientColumns} FROM clients WHERE id=@id", id, ClientRow);
    public Field? GetField(Guid id) => Find($"SELECT {FieldColumns} FROM fields WHERE id=@id", id, FieldRow);
    public Plot? GetPlot(Guid id) => Find($"SELECT {PlotColumns} FROM plots WHERE id=@id", id, PlotRow);

    private IReadOnlyList<T> List<T>(string sql, Func<NpgsqlDataReader, T> map, Guid? parent = null)
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        if (parent is { } key) Add(command, "parent", key);
        using var reader = command.ExecuteReader();
        var rows = new List<T>();
        while (reader.Read()) rows.Add(map(reader));
        return rows;
    }
    public IReadOnlyList<Client> ListClients() => List($"SELECT {ClientColumns} FROM clients ORDER BY name,id", ClientRow);
    public IReadOnlyList<Field> ListFields(Guid? clientId) => List(
        $"SELECT {FieldColumns} FROM fields {(clientId is null ? "" : "WHERE client_id=@parent ")}ORDER BY name,id", FieldRow, clientId);
    public IReadOnlyList<Plot> ListPlots(Guid? fieldId) => List(
        $"SELECT {PlotColumns} FROM plots {(fieldId is null ? "" : "WHERE field_id=@parent ")}ORDER BY name,id", PlotRow, fieldId);
}
