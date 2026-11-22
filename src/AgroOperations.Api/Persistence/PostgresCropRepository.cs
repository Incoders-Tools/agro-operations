using AgroOperations.Api.Domain;
using Npgsql;

namespace AgroOperations.Api.Persistence;

public sealed class PostgresCropRepository(string connectionString) : ICropRepository
{
    private static Crop Map(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetString(1), r.GetInt64(2));

    public Crop Create(string name)
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand("INSERT INTO crops(id,name) VALUES (@id,@name) RETURNING id,name,revision", db);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("name", name);
        try
        {
            using var row = cmd.ExecuteReader();
            row.Read();
            return Map(row);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DomainValidationException("Crop already exists");
        }
    }

    public Crop Update(Guid id, long revision, string name)
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand("UPDATE crops SET name=@name,revision=revision+1 WHERE id=@id AND revision=@revision RETURNING id,name,revision", db);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("revision", revision);
        cmd.Parameters.AddWithValue("name", name);
        try
        {
            using (var row = cmd.ExecuteReader())
                if (row.Read()) return Map(row);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DomainValidationException("Crop already exists");
        }
        using var exists = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM crops WHERE id=@id)", db);
        exists.Parameters.AddWithValue("id", id);
        if ((bool)exists.ExecuteScalar()!) throw new DomainConflictException();
        throw new DomainNotFoundException();
    }

    public IReadOnlyList<Crop> List()
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand("SELECT id,name,revision FROM crops ORDER BY name,id", db);
        using var row = cmd.ExecuteReader();
        var crops = new List<Crop>();
        while (row.Read()) crops.Add(Map(row));
        return crops;
    }
}
