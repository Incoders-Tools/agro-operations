using AgroOperations.Api.Domain;
using Npgsql;

namespace AgroOperations.Api.Persistence;

public sealed class PostgresInputRepository(string connectionString) : IInputRepository
{
    private const string Columns =
        "id,commercial_name,active_ingredient,concentration,formulation,mix_order,category,unit,dose_min,dose_max,reference_cost,hollow_cone,revision";

    private static string? Text(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
    private static decimal? Number(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetDecimal(i);
    private static InputProduct Map(NpgsqlDataReader r) =>
        new(r.GetGuid(0), r.GetString(1), Text(r, 2), Text(r, 3), Text(r, 4), r.IsDBNull(5) ? null : r.GetInt16(5), Text(r, 6),
            r.GetString(7), Number(r, 8), Number(r, 9), Number(r, 10), r.GetBoolean(11), r.GetInt64(12));

    private static void Bind(NpgsqlCommand cmd, InputDraft input)
    {
        cmd.Parameters.AddWithValue("commercialName", input.CommercialName);
        cmd.Parameters.AddWithValue("activeIngredient", (object?)input.ActiveIngredient ?? DBNull.Value);
        cmd.Parameters.AddWithValue("concentration", (object?)input.Concentration ?? DBNull.Value);
        cmd.Parameters.AddWithValue("formulation", (object?)input.Formulation ?? DBNull.Value);
        cmd.Parameters.AddWithValue("mixOrder", (object?)input.MixOrder ?? DBNull.Value);
        cmd.Parameters.AddWithValue("category", (object?)input.Category ?? DBNull.Value);
        cmd.Parameters.AddWithValue("unit", input.Unit);
        cmd.Parameters.AddWithValue("doseMin", (object?)input.DoseMin ?? DBNull.Value);
        cmd.Parameters.AddWithValue("doseMax", (object?)input.DoseMax ?? DBNull.Value);
        cmd.Parameters.AddWithValue("referenceCost", (object?)input.ReferenceCost ?? DBNull.Value);
        cmd.Parameters.AddWithValue("hollowCone", input.HollowCone);
    }

    public InputProduct Create(InputDraft input)
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand($"""
            INSERT INTO inputs(id,commercial_name,active_ingredient,concentration,formulation,mix_order,category,unit,dose_min,dose_max,reference_cost,hollow_cone)
            VALUES (@id,@commercialName,@activeIngredient,@concentration,@formulation,@mixOrder,@category,@unit,@doseMin,@doseMax,@referenceCost,@hollowCone)
            RETURNING {Columns}
            """, db);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        Bind(cmd, input);
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

    public InputProduct Update(Guid id, long revision, InputDraft input)
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand($"""
            UPDATE inputs SET commercial_name=@commercialName,active_ingredient=@activeIngredient,concentration=@concentration,
              formulation=@formulation,mix_order=@mixOrder,category=@category,unit=@unit,dose_min=@doseMin,dose_max=@doseMax,
              reference_cost=@referenceCost,hollow_cone=@hollowCone,revision=revision+1
            WHERE id=@id AND revision=@revision RETURNING {Columns}
            """, db);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("revision", revision);
        Bind(cmd, input);
        try
        {
            using (var row = cmd.ExecuteReader())
                if (row.Read()) return Map(row);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DomainDuplicateException();
        }
        using var exists = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM inputs WHERE id=@id)", db);
        exists.Parameters.AddWithValue("id", id);
        if ((bool)exists.ExecuteScalar()!) throw new DomainConflictException();
        throw new DomainNotFoundException();
    }

    public IReadOnlyList<InputProduct> List()
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand($"SELECT {Columns} FROM inputs ORDER BY commercial_name,id", db);
        using var row = cmd.ExecuteReader();
        var inputs = new List<InputProduct>();
        while (row.Read()) inputs.Add(Map(row));
        return inputs;
    }
}
