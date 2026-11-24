using AgroOperations.Api.Domain;
using Npgsql;
using NpgsqlTypes;

namespace AgroOperations.Api.Persistence;

/// <summary>
/// Every write runs in one transaction; the laboratory row is share-locked while its kind is checked, so its kind cannot
/// change before the write commits.
/// </summary>
public sealed class PostgresSeedAnalysisRepository(string connectionString) : ISeedAnalysisRepository
{
    private const string Columns = """
        id,client_id,crop_id,variety,campaign_id,analyzed_on,laboratory_id,thousand_seed_weight_g,germination_treated_pct,abnormal_treated_pct,dead_treated_pct,
        hard_treated_pct,germination_untreated_pct,abnormal_untreated_pct,dead_untreated_pct,hard_untreated_pct,vigor_pct,notes,revision
        """;

    private static string? Text(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
    private static Guid? Id(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetGuid(i);
    private static decimal? Number(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetDecimal(i);
    private static NpgsqlParameter Uuid(string name, Guid? value) => new(name, NpgsqlDbType.Uuid) { Value = (object?)value ?? DBNull.Value };
    private static NpgsqlParameter Varchar(string name, string? value) => new(name, NpgsqlDbType.Text) { Value = (object?)value ?? DBNull.Value };
    private static NpgsqlParameter Numeric(string name, decimal? value) => new(name, NpgsqlDbType.Numeric) { Value = (object?)value ?? DBNull.Value };

    private static SeedAnalysis Map(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), Text(r, 3), Id(r, 4), r.GetFieldValue<DateOnly>(5), Id(r, 6),
        Number(r, 7), Number(r, 8), Number(r, 9), Number(r, 10), Number(r, 11), Number(r, 12), Number(r, 13), Number(r, 14), Number(r, 15), Number(r, 16), Text(r, 17),
        r.GetInt64(18));

    private NpgsqlConnection Open()
    {
        var db = new NpgsqlConnection(connectionString);
        db.Open();
        return db;
    }

    public SeedAnalysis Create(SeedAnalysisDraft analysis)
    {
        using var db = Open();
        using var tx = db.BeginTransaction();
        References(db, tx, analysis);
        using var insert = new NpgsqlCommand($"""
            INSERT INTO seed_analyses({Columns.Replace(",revision", "")})
            VALUES (@id,@client,@crop,@variety,@campaign,@analyzedOn,@laboratory,@weight,@germinationTreated,@abnormalTreated,@deadTreated,@hardTreated,
              @germinationUntreated,@abnormalUntreated,@deadUntreated,@hardUntreated,@vigor,@notes)
            RETURNING {Columns}
            """, db, tx);
        insert.Parameters.AddWithValue("id", Guid.NewGuid());
        Bind(insert, analysis);
        SeedAnalysis created;
        using (var row = insert.ExecuteReader())
        {
            row.Read();
            created = Map(row);
        }
        tx.Commit();
        return created;
    }

    public SeedAnalysis Update(Guid id, long revision, SeedAnalysisDraft analysis)
    {
        using var db = Open();
        using var tx = db.BeginTransaction();
        using (var current = new NpgsqlCommand("SELECT revision FROM seed_analyses WHERE id=@id FOR UPDATE", db, tx))
        {
            current.Parameters.AddWithValue("id", id);
            var stored = current.ExecuteScalar() ?? throw new DomainNotFoundException();
            if ((long)stored != revision) throw new DomainConflictException();
        }
        References(db, tx, analysis);
        using var update = new NpgsqlCommand($"""
            UPDATE seed_analyses SET client_id=@client,crop_id=@crop,variety=@variety,campaign_id=@campaign,analyzed_on=@analyzedOn,laboratory_id=@laboratory,
              thousand_seed_weight_g=@weight,germination_treated_pct=@germinationTreated,abnormal_treated_pct=@abnormalTreated,dead_treated_pct=@deadTreated,
              hard_treated_pct=@hardTreated,germination_untreated_pct=@germinationUntreated,abnormal_untreated_pct=@abnormalUntreated,
              dead_untreated_pct=@deadUntreated,hard_untreated_pct=@hardUntreated,vigor_pct=@vigor,notes=@notes,revision=revision+1
            WHERE id=@id RETURNING {Columns}
            """, db, tx);
        update.Parameters.AddWithValue("id", id);
        Bind(update, analysis);
        SeedAnalysis updated;
        using (var row = update.ExecuteReader())
        {
            row.Read();
            updated = Map(row);
        }
        tx.Commit();
        return updated;
    }

    public SeedAnalysis? Get(Guid id)
    {
        using var db = Open();
        using var cmd = new NpgsqlCommand($"SELECT {Columns} FROM seed_analyses WHERE id=@id", db);
        cmd.Parameters.AddWithValue("id", id);
        using var row = cmd.ExecuteReader();
        return row.Read() ? Map(row) : null;
    }

    public IReadOnlyList<SeedAnalysis> List(Guid? clientId, Guid? cropId)
    {
        using var db = Open();
        using var cmd = new NpgsqlCommand($"""
            SELECT {Columns} FROM seed_analyses WHERE (@client IS NULL OR client_id=@client) AND (@crop IS NULL OR crop_id=@crop)
            ORDER BY analyzed_on DESC,created_at DESC,id
            """, db);
        cmd.Parameters.Add(Uuid("client", clientId));
        cmd.Parameters.Add(Uuid("crop", cropId));
        using var row = cmd.ExecuteReader();
        var analyses = new List<SeedAnalysis>();
        while (row.Read()) analyses.Add(Map(row));
        return analyses;
    }

    private static void Bind(NpgsqlCommand cmd, SeedAnalysisDraft analysis)
    {
        cmd.Parameters.AddWithValue("client", analysis.ClientId!.Value);
        cmd.Parameters.AddWithValue("crop", analysis.CropId!.Value);
        cmd.Parameters.Add(Varchar("variety", analysis.Variety));
        cmd.Parameters.Add(Uuid("campaign", analysis.CampaignId));
        cmd.Parameters.AddWithValue("analyzedOn", analysis.AnalyzedOn!.Value);
        cmd.Parameters.Add(Uuid("laboratory", analysis.LaboratoryId));
        cmd.Parameters.Add(Numeric("weight", analysis.ThousandSeedWeightG));
        cmd.Parameters.Add(Numeric("germinationTreated", analysis.GerminationTreatedPct));
        cmd.Parameters.Add(Numeric("abnormalTreated", analysis.AbnormalTreatedPct));
        cmd.Parameters.Add(Numeric("deadTreated", analysis.DeadTreatedPct));
        cmd.Parameters.Add(Numeric("hardTreated", analysis.HardTreatedPct));
        cmd.Parameters.Add(Numeric("germinationUntreated", analysis.GerminationUntreatedPct));
        cmd.Parameters.Add(Numeric("abnormalUntreated", analysis.AbnormalUntreatedPct));
        cmd.Parameters.Add(Numeric("deadUntreated", analysis.DeadUntreatedPct));
        cmd.Parameters.Add(Numeric("hardUntreated", analysis.HardUntreatedPct));
        cmd.Parameters.Add(Numeric("vigor", analysis.VigorPct));
        cmd.Parameters.Add(Varchar("notes", analysis.Notes));
    }

    /// <summary>A missing client, crop, campaign or laboratory is not found; a provider that is not a seed laboratory is rejected.</summary>
    private static void References(NpgsqlConnection db, NpgsqlTransaction tx, SeedAnalysisDraft analysis)
    {
        using (var exists = new NpgsqlCommand("""
            SELECT EXISTS(SELECT 1 FROM clients WHERE id=@client) AND EXISTS(SELECT 1 FROM crops WHERE id=@crop)
              AND (@campaign IS NULL OR EXISTS(SELECT 1 FROM campaigns WHERE id=@campaign))
            """, db, tx))
        {
            exists.Parameters.AddWithValue("client", analysis.ClientId!.Value);
            exists.Parameters.AddWithValue("crop", analysis.CropId!.Value);
            exists.Parameters.Add(Uuid("campaign", analysis.CampaignId));
            if (exists.ExecuteScalar() is not true) throw new DomainNotFoundException();
        }
        if (analysis.LaboratoryId is not { } laboratoryId) return;
        using var kind = new NpgsqlCommand("SELECT kind FROM providers WHERE id=@id FOR SHARE", db, tx);
        kind.Parameters.AddWithValue("id", laboratoryId);
        var found = kind.ExecuteScalar() ?? throw new DomainNotFoundException();
        if ((string)found != SeedAnalysisService.SeedLaboratory) throw new DomainValidationException("Laboratory must be a seed laboratory");
    }
}
