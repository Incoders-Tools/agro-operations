using AgroOperations.Api.Domain;
using Npgsql;

namespace AgroOperations.Api.Persistence;

public sealed class PostgresStageRepository(string connectionString) : IStageRepository
{
    private static Stage Map(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetString(1), r.GetInt32(2), r.GetInt64(3));

    public Stage Create(string name, int sortOrder)
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand("INSERT INTO stages(id,name,sort_order) VALUES (@id,@name,@sortOrder) RETURNING id,name,sort_order,revision", db);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("sortOrder", sortOrder);
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

    public Stage Update(Guid id, long revision, string name, int sortOrder)
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand("UPDATE stages SET name=@name,sort_order=@sortOrder,revision=revision+1 WHERE id=@id AND revision=@revision RETURNING id,name,sort_order,revision", db);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("revision", revision);
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("sortOrder", sortOrder);
        try
        {
            using (var row = cmd.ExecuteReader())
                if (row.Read()) return Map(row);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DomainDuplicateException();
        }
        using var exists = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM stages WHERE id=@id)", db);
        exists.Parameters.AddWithValue("id", id);
        if ((bool)exists.ExecuteScalar()!) throw new DomainConflictException();
        throw new DomainNotFoundException();
    }

    public IReadOnlyList<Stage> List()
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand("SELECT id,name,sort_order,revision FROM stages ORDER BY sort_order,name,id", db);
        using var row = cmd.ExecuteReader();
        var stages = new List<Stage>();
        while (row.Read()) stages.Add(Map(row));
        return stages;
    }
}
