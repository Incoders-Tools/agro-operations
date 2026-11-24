using AgroOperations.Api.Domain;
using Npgsql;

namespace AgroOperations.Api.Persistence;

public sealed class PostgresCommercialFirmRepository(string connectionString) : ICommercialFirmRepository
{
    private const string Columns = "id,client_id,name,tax_id,revision";

    private static CommercialFirm Map(NpgsqlDataReader r) =>
        new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3), r.GetInt64(4));

    public CommercialFirm Create(Guid clientId, string name, string? taxId)
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand($"INSERT INTO commercial_firms(id,client_id,name,tax_id) SELECT @id,id,@name,@taxId FROM clients WHERE id=@client RETURNING {Columns}", db);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("client", clientId);
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("taxId", (object?)taxId ?? DBNull.Value);
        try
        {
            using var row = cmd.ExecuteReader();
            if (!row.Read()) throw new DomainNotFoundException();
            return Map(row);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DomainDuplicateException();
        }
    }

    public CommercialFirm Update(Guid id, long revision, string name, string? taxId)
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand($"UPDATE commercial_firms SET name=@name,tax_id=@taxId,revision=revision+1 WHERE id=@id AND revision=@revision RETURNING {Columns}", db);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("revision", revision);
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("taxId", (object?)taxId ?? DBNull.Value);
        try
        {
            using (var row = cmd.ExecuteReader())
                if (row.Read()) return Map(row);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DomainDuplicateException();
        }
        using var exists = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM commercial_firms WHERE id=@id)", db);
        exists.Parameters.AddWithValue("id", id);
        if ((bool)exists.ExecuteScalar()!) throw new DomainConflictException();
        throw new DomainNotFoundException();
    }

    public IReadOnlyList<CommercialFirm> List(Guid? clientId)
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand($"SELECT {Columns} FROM commercial_firms {(clientId is null ? "" : "WHERE client_id=@client ")}ORDER BY name,id", db);
        if (clientId is { } client) cmd.Parameters.AddWithValue("client", client);
        using var row = cmd.ExecuteReader();
        var firms = new List<CommercialFirm>();
        while (row.Read()) firms.Add(Map(row));
        return firms;
    }
}
