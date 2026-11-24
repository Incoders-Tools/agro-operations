using AgroOperations.Api.Domain;
using Npgsql;

namespace AgroOperations.Api.Persistence;

public sealed class PostgresPlotCropRepository(string connectionString) : IPlotCropRepository
{
    private const string Columns = "id,plot_id,campaign_id,crop_id,position,role,variety,grain_yield_kg_ha,forage_biomass_kg_ha,revision";

    private static readonly string UpdatedColumns = string.Join(",", Columns.Split(',').Select(column => "plot_crops." + column));

    private static decimal? Number(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetDecimal(i);
    private static PlotCrop Map(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetGuid(3), r.GetInt16(4),
        r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6), Number(r, 7), Number(r, 8), r.GetInt64(9));

    private static void BindYields(NpgsqlCommand cmd, decimal? grainYieldKgHa, decimal? forageBiomassKgHa)
    {
        cmd.Parameters.AddWithValue("grain", (object?)grainYieldKgHa ?? DBNull.Value);
        cmd.Parameters.AddWithValue("forage", (object?)forageBiomassKgHa ?? DBNull.Value);
    }

    public PlotCrop Create(Guid plotId, Guid campaignId, Guid cropId, short position, string role, string? variety, decimal? grainYieldKgHa, decimal? forageBiomassKgHa)
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand($"""
            INSERT INTO plot_crops(id,plot_id,campaign_id,crop_id,position,role,variety,grain_yield_kg_ha,forage_biomass_kg_ha)
            SELECT @id, p.id, c.id, k.id, @position, @role, @variety, @grain, @forage FROM plots p, campaigns c, crops k
            WHERE p.id=@plot AND c.id=@campaign AND k.id=@crop
            RETURNING {Columns}
            """, db);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("plot", plotId);
        cmd.Parameters.AddWithValue("campaign", campaignId);
        cmd.Parameters.AddWithValue("crop", cropId);
        cmd.Parameters.AddWithValue("position", position);
        cmd.Parameters.AddWithValue("role", role);
        cmd.Parameters.AddWithValue("variety", (object?)variety ?? DBNull.Value);
        BindYields(cmd, grainYieldKgHa, forageBiomassKgHa);
        try
        {
            using var row = cmd.ExecuteReader();
            if (!row.Read()) throw new DomainNotFoundException();
            return Map(row);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DomainDuplicateException("Duplicate position");
        }
    }

    public PlotCrop Update(Guid id, long revision, Guid cropId, string role, string? variety, decimal? grainYieldKgHa, decimal? forageBiomassKgHa)
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand($"""
            UPDATE plot_crops SET crop_id=k.id,role=@role,variety=@variety,grain_yield_kg_ha=@grain,forage_biomass_kg_ha=@forage,revision=plot_crops.revision+1
            FROM crops k WHERE plot_crops.id=@id AND plot_crops.revision=@revision AND k.id=@crop
            RETURNING {UpdatedColumns}
            """, db);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("revision", revision);
        cmd.Parameters.AddWithValue("crop", cropId);
        cmd.Parameters.AddWithValue("role", role);
        cmd.Parameters.AddWithValue("variety", (object?)variety ?? DBNull.Value);
        BindYields(cmd, grainYieldKgHa, forageBiomassKgHa);
        using (var row = cmd.ExecuteReader())
            if (row.Read()) return Map(row);
        // A missing record or crop is not found; only an existing record with an existing crop is stale.
        using var exists = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM plot_crops WHERE id=@id) AND EXISTS(SELECT 1 FROM crops WHERE id=@crop)", db);
        exists.Parameters.AddWithValue("id", id);
        exists.Parameters.AddWithValue("crop", cropId);
        if ((bool)exists.ExecuteScalar()!) throw new DomainConflictException();
        throw new DomainNotFoundException();
    }

    public IReadOnlyList<PlotCrop> List(Guid? plotId)
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand($"SELECT {Columns} FROM plot_crops {(plotId is null ? "" : "WHERE plot_id=@plot ")}ORDER BY plot_id,campaign_id,position,id", db);
        if (plotId is { } plot) cmd.Parameters.AddWithValue("plot", plot);
        using var row = cmd.ExecuteReader();
        var plotCrops = new List<PlotCrop>();
        while (row.Read()) plotCrops.Add(Map(row));
        return plotCrops;
    }
}
