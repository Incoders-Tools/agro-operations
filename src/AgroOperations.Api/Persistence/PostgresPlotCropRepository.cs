using AgroOperations.Api.Domain;
using Npgsql;

namespace AgroOperations.Api.Persistence;

public sealed class PostgresPlotCropRepository(string connectionString) : IPlotCropRepository
{
    private const string Columns = "id,plot_id,campaign_id,crop_id,position,role,variety,revision";

    private static PlotCrop Map(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetGuid(3), r.GetInt16(4),
        r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6), r.GetInt64(7));

    public PlotCrop Create(Guid plotId, Guid campaignId, Guid cropId, short position, string role, string? variety)
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand($"""
            INSERT INTO plot_crops(id,plot_id,campaign_id,crop_id,position,role,variety)
            SELECT @id, p.id, c.id, k.id, @position, @role, @variety FROM plots p, campaigns c, crops k
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

    public PlotCrop Update(Guid id, long revision, Guid cropId, string role, string? variety)
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand($"""
            UPDATE plot_crops SET crop_id=k.id,role=@role,variety=@variety,revision=plot_crops.revision+1
            FROM crops k WHERE plot_crops.id=@id AND plot_crops.revision=@revision AND k.id=@crop
            RETURNING plot_crops.id,plot_crops.plot_id,plot_crops.campaign_id,plot_crops.crop_id,plot_crops.position,plot_crops.role,plot_crops.variety,plot_crops.revision
            """, db);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("revision", revision);
        cmd.Parameters.AddWithValue("crop", cropId);
        cmd.Parameters.AddWithValue("role", role);
        cmd.Parameters.AddWithValue("variety", (object?)variety ?? DBNull.Value);
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
