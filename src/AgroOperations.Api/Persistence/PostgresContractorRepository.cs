using AgroOperations.Api.Domain;
using Npgsql;

namespace AgroOperations.Api.Persistence;

public sealed class PostgresContractorRepository(string connectionString) : IContractorRepository
{
    private const string Columns = "id,name,tax_id,phone,email,notes,own_equipment,revision,website";

    private static string? Text(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
    private static Contractor Map(NpgsqlDataReader r) =>
        new(r.GetGuid(0), r.GetString(1), Text(r, 2), Text(r, 3), Text(r, 4), Text(r, 5), r.GetBoolean(6), r.GetInt64(7), Text(r, 8));

    private static void Bind(NpgsqlCommand cmd, ContractorDraft contractor)
    {
        cmd.Parameters.AddWithValue("name", contractor.Name);
        cmd.Parameters.AddWithValue("taxId", (object?)contractor.TaxId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("phone", (object?)contractor.Phone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("email", (object?)contractor.Email ?? DBNull.Value);
        cmd.Parameters.AddWithValue("notes", (object?)contractor.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ownEquipment", contractor.OwnEquipment);
        cmd.Parameters.AddWithValue("website", (object?)contractor.Website ?? DBNull.Value);
    }

    public Contractor Create(ContractorDraft contractor)
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand($"INSERT INTO contractors(id,name,tax_id,phone,email,notes,own_equipment,website) VALUES (@id,@name,@taxId,@phone,@email,@notes,@ownEquipment,@website) RETURNING {Columns}", db);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        Bind(cmd, contractor);
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

    public Contractor Update(Guid id, long revision, ContractorDraft contractor)
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand($"UPDATE contractors SET name=@name,tax_id=@taxId,phone=@phone,email=@email,notes=@notes,own_equipment=@ownEquipment,website=@website,revision=revision+1 WHERE id=@id AND revision=@revision RETURNING {Columns}", db);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("revision", revision);
        Bind(cmd, contractor);
        try
        {
            using (var row = cmd.ExecuteReader())
                if (row.Read()) return Map(row);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DomainDuplicateException();
        }
        using var exists = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM contractors WHERE id=@id)", db);
        exists.Parameters.AddWithValue("id", id);
        if ((bool)exists.ExecuteScalar()!) throw new DomainConflictException();
        throw new DomainNotFoundException();
    }

    public IReadOnlyList<Contractor> List()
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand($"SELECT {Columns} FROM contractors ORDER BY name,id", db);
        using var row = cmd.ExecuteReader();
        var contractors = new List<Contractor>();
        while (row.Read()) contractors.Add(Map(row));
        return contractors;
    }
}
