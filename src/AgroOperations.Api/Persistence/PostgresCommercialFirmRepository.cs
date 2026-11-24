using AgroOperations.Api.Domain;
using Npgsql;
using NpgsqlTypes;

namespace AgroOperations.Api.Persistence;

public sealed class PostgresCommercialFirmRepository(string connectionString) : ICommercialFirmRepository
{
    private const string Columns = "id,client_id,name,tax_id,next_work_order_number,revision";

    private static CommercialFirm Map(NpgsqlDataReader r) =>
        new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3), r.GetInt32(4), r.GetInt64(5));

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

    public CommercialFirm Update(Guid id, long revision, string name, string? taxId, int? nextWorkOrderNumber)
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        // The row lock waits for any work order create of this firm, so the highest number read next is final.
        using var tx = db.BeginTransaction();
        using (var current = new NpgsqlCommand("SELECT revision FROM commercial_firms WHERE id=@id FOR UPDATE", db, tx))
        {
            current.Parameters.AddWithValue("id", id);
            if (current.ExecuteScalar() is not long stored) throw new DomainNotFoundException();
            if (stored != revision) throw new DomainConflictException();
        }
        if (nextWorkOrderNumber is { } next)
        {
            using var highest = new NpgsqlCommand("SELECT coalesce(max(number),0) FROM work_orders WHERE firm_id=@id", db, tx);
            highest.Parameters.AddWithValue("id", id);
            var issued = (int)highest.ExecuteScalar()!;
            if (next <= issued) throw new DomainValidationException($"Next number must be greater than {issued}");
        }
        using var cmd = new NpgsqlCommand($"""
            UPDATE commercial_firms SET name=@name,tax_id=@taxId,
              next_work_order_number=coalesce(@next,next_work_order_number),revision=revision+1
            WHERE id=@id RETURNING {Columns}
            """, db, tx);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("taxId", (object?)taxId ?? DBNull.Value);
        cmd.Parameters.Add(new NpgsqlParameter("next", NpgsqlDbType.Integer) { Value = (object?)nextWorkOrderNumber ?? DBNull.Value });
        try
        {
            CommercialFirm firm;
            using (var row = cmd.ExecuteReader())
            {
                row.Read();
                firm = Map(row);
            }
            tx.Commit();
            return firm;
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DomainDuplicateException();
        }
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
