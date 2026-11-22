using AgroOperations.Api.Domain;
using Npgsql;
using NpgsqlTypes;

namespace AgroOperations.Api.Persistence;

public sealed class PostgresImportRecordRepository(string connectionString) : IImportRecordRepository
{
    private static ImportRecord Map(NpgsqlDataReader r) =>
        new(r.GetGuid(0), r.GetString(1), r.GetGuid(2), r.GetString(3), r.GetBoolean(4), r.GetString(5));

    public ImportRecord? Find(string entityType, Guid entityId)
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand(
            "SELECT id,entity_type,entity_id,source_reference,verified,raw_payload::text FROM import_records WHERE entity_type=@type AND entity_id=@id", db);
        cmd.Parameters.AddWithValue("type", entityType);
        cmd.Parameters.AddWithValue("id", entityId);
        using var row = cmd.ExecuteReader();
        return row.Read() ? Map(row) : null;
    }

    public ImportRecord Create(string entityType, Guid entityId, string sourceReference, string rawPayloadJson)
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand(
            "INSERT INTO import_records(id,entity_type,entity_id,source_reference,raw_payload) VALUES (@id,@type,@entityId,@source,@payload::jsonb) RETURNING id,entity_type,entity_id,source_reference,verified,raw_payload::text", db);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("type", entityType);
        cmd.Parameters.AddWithValue("entityId", entityId);
        cmd.Parameters.AddWithValue("source", sourceReference);
        cmd.Parameters.Add(new NpgsqlParameter("payload", NpgsqlDbType.Jsonb) { Value = rawPayloadJson });
        using var row = cmd.ExecuteReader();
        row.Read();
        return Map(row);
    }

    public IReadOnlyList<ImportRecord> List()
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand("SELECT id,entity_type,entity_id,source_reference,verified,raw_payload::text FROM import_records ORDER BY id", db);
        using var row = cmd.ExecuteReader();
        var records = new List<ImportRecord>();
        while (row.Read()) records.Add(Map(row));
        return records;
    }
}
