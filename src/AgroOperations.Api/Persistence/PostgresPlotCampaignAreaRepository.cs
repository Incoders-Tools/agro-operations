using AgroOperations.Api.Domain;
using Npgsql;

namespace AgroOperations.Api.Persistence;

public sealed class PostgresPlotCampaignAreaRepository(string connectionString) : IPlotCampaignAreaRepository
{
    private static PlotCampaignArea Map(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetDecimal(3), r.GetInt64(4));

    public PlotCampaignArea Create(Guid plotId, Guid campaignId, decimal areaHectares)
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand("""
            INSERT INTO plot_campaign_areas(id,plot_id,campaign_id,area_hectares)
            SELECT @id, p.id, c.id, @area FROM plots p, campaigns c WHERE p.id=@plot AND c.id=@campaign
            RETURNING id,plot_id,campaign_id,area_hectares,revision
            """, db);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("plot", plotId);
        cmd.Parameters.AddWithValue("campaign", campaignId);
        cmd.Parameters.AddWithValue("area", areaHectares);
        try
        {
            using var row = cmd.ExecuteReader();
            if (!row.Read()) throw new DomainNotFoundException();
            return Map(row);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DomainValidationException("Area already recorded for this plot and campaign");
        }
    }

    public PlotCampaignArea Update(Guid id, long revision, decimal areaHectares)
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand("UPDATE plot_campaign_areas SET area_hectares=@area,revision=revision+1 WHERE id=@id AND revision=@revision RETURNING id,plot_id,campaign_id,area_hectares,revision", db);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("revision", revision);
        cmd.Parameters.AddWithValue("area", areaHectares);
        using (var row = cmd.ExecuteReader())
            if (row.Read()) return Map(row);
        using var exists = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM plot_campaign_areas WHERE id=@id)", db);
        exists.Parameters.AddWithValue("id", id);
        if ((bool)exists.ExecuteScalar()!) throw new DomainConflictException();
        throw new DomainNotFoundException();
    }

    public IReadOnlyList<PlotCampaignArea> List()
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand("SELECT id,plot_id,campaign_id,area_hectares,revision FROM plot_campaign_areas ORDER BY id", db);
        using var row = cmd.ExecuteReader();
        var areas = new List<PlotCampaignArea>();
        while (row.Read()) areas.Add(Map(row));
        return areas;
    }
}
