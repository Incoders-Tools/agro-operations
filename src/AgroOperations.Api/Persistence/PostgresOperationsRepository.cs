using AgroOperations.Api.Domain;
using Npgsql;

namespace AgroOperations.Api.Persistence;

/// <summary>One statement per mutation; revision predicates make updates atomic across processes.</summary>
public sealed class PostgresOperationsRepository(string connectionString) : IOperationsRepository
{
    private T Execute<T>(string sql, Action<NpgsqlCommand> parameters, Func<NpgsqlDataReader, T> map)
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        parameters(command);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) throw new DomainNotFoundException();
        return map(reader);
    }

    private static void Add(NpgsqlCommand command, string key, object value) => command.Parameters.AddWithValue(key, value);
    private static Client ClientRow(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetString(1), r.GetInt64(2));
    private static Field FieldRow(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetInt64(3));
    private static Plot PlotRow(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetDecimal(3), r.GetInt64(4));

    public Client CreateClient(string name) => Execute(
        "INSERT INTO clients(id,name) VALUES (@id,@name) RETURNING id,name,revision",
        c => { Add(c, "id", Guid.NewGuid()); Add(c, "name", name); }, ClientRow);

    public Field CreateField(Guid clientId, string name) => Execute(
        "INSERT INTO fields(id,client_id,name) SELECT @id,id,@name FROM clients WHERE id=@parent RETURNING id,client_id,name,revision",
        c => { Add(c, "id", Guid.NewGuid()); Add(c, "parent", clientId); Add(c, "name", name); }, FieldRow);

    public Plot CreatePlot(Guid fieldId, string name, decimal areaHectares) => Execute(
        "INSERT INTO plots(id,field_id,name,area_hectares) SELECT @id,id,@name,@area FROM fields WHERE id=@parent RETURNING id,field_id,name,area_hectares,revision",
        c => { Add(c, "id", Guid.NewGuid()); Add(c, "parent", fieldId); Add(c, "name", name); Add(c, "area", areaHectares); }, PlotRow);

    private T Update<T>(string sql, Guid id, long revision, Action<NpgsqlCommand> extra, Func<NpgsqlDataReader, T> map, Func<Guid, bool> exists)
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        Add(command, "id", id);
        Add(command, "expected", revision);
        extra(command);
        using (var reader = command.ExecuteReader())
            if (reader.Read()) return map(reader);
        if (exists(id)) throw new DomainConflictException();
        throw new DomainNotFoundException();
    }

    public Client UpdateClient(Guid id, long revision, string name) => Update(
        "UPDATE clients SET name=@name,revision=revision+1 WHERE id=@id AND revision=@expected RETURNING id,name,revision",
        id, revision, c => Add(c, "name", name), ClientRow, key => GetClient(key) is not null);
    public Field UpdateField(Guid id, long revision, string name) => Update(
        "UPDATE fields SET name=@name,revision=revision+1 WHERE id=@id AND revision=@expected RETURNING id,client_id,name,revision",
        id, revision, c => Add(c, "name", name), FieldRow, key => GetField(key) is not null);
    public Plot UpdatePlot(Guid id, long revision, string name, decimal areaHectares) => Update(
        "UPDATE plots SET name=@name,area_hectares=@area,revision=revision+1 WHERE id=@id AND revision=@expected RETURNING id,field_id,name,area_hectares,revision",
        id, revision, c => { Add(c, "name", name); Add(c, "area", areaHectares); }, PlotRow, key => GetPlot(key) is not null);

    private T? Find<T>(string sql, Guid id, Func<NpgsqlDataReader, T> map) where T : class
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        Add(command, "id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? map(reader) : null;
    }
    public Client? GetClient(Guid id) => Find("SELECT id,name,revision FROM clients WHERE id=@id", id, ClientRow);
    public Field? GetField(Guid id) => Find("SELECT id,client_id,name,revision FROM fields WHERE id=@id", id, FieldRow);
    public Plot? GetPlot(Guid id) => Find("SELECT id,field_id,name,area_hectares,revision FROM plots WHERE id=@id", id, PlotRow);
}
