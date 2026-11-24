using AgroOperations.Api.Domain;
using Npgsql;

namespace AgroOperations.Api.Persistence;

public sealed class PostgresCampaignRepository(string connectionString) : ICampaignRepository
{
    private static Campaign Map(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetString(1), r.GetInt64(2));

    public Campaign Create(string name)
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand("INSERT INTO campaigns(id,name) VALUES (@id,@name) RETURNING id,name,revision", db);
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
            throw new DomainDuplicateException();
        }
    }

    public Campaign Update(Guid id, long revision, string name)
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand("UPDATE campaigns SET name=@name,revision=revision+1 WHERE id=@id AND revision=@revision RETURNING id,name,revision", db);
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
            throw new DomainDuplicateException();
        }
        using var exists = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM campaigns WHERE id=@id)", db);
        exists.Parameters.AddWithValue("id", id);
        if ((bool)exists.ExecuteScalar()!) throw new DomainConflictException();
        throw new DomainNotFoundException();
    }

    public IReadOnlyList<Campaign> List()
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand("SELECT id,name,revision FROM campaigns ORDER BY name,id", db);
        using var row = cmd.ExecuteReader();
        var campaigns = new List<Campaign>();
        while (row.Read()) campaigns.Add(Map(row));
        return campaigns;
    }
}
