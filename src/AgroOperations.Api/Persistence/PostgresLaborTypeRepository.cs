using AgroOperations.Api.Domain;
using Npgsql;

namespace AgroOperations.Api.Persistence;

public sealed class PostgresLaborTypeRepository(string connectionString) : ILaborTypeRepository
{
    private const string Columns = "id,name,abbreviation,category,description,revision";

    private static string? Text(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
    private static LaborType Map(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetString(1), Text(r, 2), Text(r, 3), Text(r, 4), r.GetInt64(5));

    private static void Bind(NpgsqlCommand cmd, string name, string? abbreviation, string? category, string? description)
    {
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("abbreviation", (object?)abbreviation ?? DBNull.Value);
        cmd.Parameters.AddWithValue("category", (object?)category ?? DBNull.Value);
        cmd.Parameters.AddWithValue("description", (object?)description ?? DBNull.Value);
    }

    public LaborType Create(string name, string? abbreviation, string? category, string? description)
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand($"INSERT INTO labor_types(id,name,abbreviation,category,description) VALUES (@id,@name,@abbreviation,@category,@description) RETURNING {Columns}", db);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        Bind(cmd, name, abbreviation, category, description);
        try
        {
            using var row = cmd.ExecuteReader();
            row.Read();
            return Map(row);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DomainDuplicateException();
        }
    }

    public LaborType Update(Guid id, long revision, string name, string? abbreviation, string? category, string? description)
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand($"UPDATE labor_types SET name=@name,abbreviation=@abbreviation,category=@category,description=@description,revision=revision+1 WHERE id=@id AND revision=@revision RETURNING {Columns}", db);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("revision", revision);
        Bind(cmd, name, abbreviation, category, description);
        try
        {
            using (var row = cmd.ExecuteReader())
                if (row.Read()) return Map(row);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DomainDuplicateException();
        }
        using var exists = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM labor_types WHERE id=@id)", db);
        exists.Parameters.AddWithValue("id", id);
        if ((bool)exists.ExecuteScalar()!) throw new DomainConflictException();
        throw new DomainNotFoundException();
    }

    public IReadOnlyList<LaborType> List()
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand($"SELECT {Columns} FROM labor_types ORDER BY name,id", db);
        using var row = cmd.ExecuteReader();
        var laborTypes = new List<LaborType>();
        while (row.Read()) laborTypes.Add(Map(row));
        return laborTypes;
    }
}
