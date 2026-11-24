using AgroOperations.Api.Domain;
using Npgsql;

namespace AgroOperations.Api.Persistence;

public sealed class PostgresLaborTypeRepository(string connectionString) : ILaborTypeRepository
{
    private static LaborType Map(NpgsqlDataReader r) =>
        new(r.GetGuid(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), r.GetInt64(3));

    public LaborType Create(string name, string? abbreviation)
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand("INSERT INTO labor_types(id,name,abbreviation) VALUES (@id,@name,@abbreviation) RETURNING id,name,abbreviation,revision", db);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("abbreviation", (object?)abbreviation ?? DBNull.Value);
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

    public LaborType Update(Guid id, long revision, string name, string? abbreviation)
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand("UPDATE labor_types SET name=@name,abbreviation=@abbreviation,revision=revision+1 WHERE id=@id AND revision=@revision RETURNING id,name,abbreviation,revision", db);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("revision", revision);
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("abbreviation", (object?)abbreviation ?? DBNull.Value);
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
        using var cmd = new NpgsqlCommand("SELECT id,name,abbreviation,revision FROM labor_types ORDER BY name,id", db);
        using var row = cmd.ExecuteReader();
        var laborTypes = new List<LaborType>();
        while (row.Read()) laborTypes.Add(Map(row));
        return laborTypes;
    }
}
