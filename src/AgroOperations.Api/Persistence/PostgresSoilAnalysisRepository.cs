using AgroOperations.Api.Domain;
using Npgsql;
using NpgsqlTypes;

namespace AgroOperations.Api.Persistence;

/// <summary>
/// Every write runs in one transaction that replaces the analysis and its results, so a rejected request leaves both unchanged.
/// The laboratory row is share-locked while its kind is checked, so its kind cannot change before the write commits.
/// </summary>
public sealed class PostgresSoilAnalysisRepository(string connectionString) : ISoilAnalysisRepository
{
    private const string AnalysisColumns =
        "a.id,a.plot_id,a.campaign_id,a.sampled_on,a.analyzed_on,a.laboratory_id,a.sampling_method,a.sampling_detail,a.map_link,a.notes,a.revision";

    private static string? Text(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
    private static Guid? Id(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetGuid(i);
    private static NpgsqlParameter Uuid(string name, Guid? value) => new(name, NpgsqlDbType.Uuid) { Value = (object?)value ?? DBNull.Value };
    private static NpgsqlParameter Varchar(string name, string? value) => new(name, NpgsqlDbType.Text) { Value = (object?)value ?? DBNull.Value };
    private static NpgsqlParameter Day(string name, DateOnly? value) => new(name, NpgsqlDbType.Date) { Value = (object?)value ?? DBNull.Value };

    private NpgsqlConnection Open()
    {
        var db = new NpgsqlConnection(connectionString);
        db.Open();
        return db;
    }

    public SoilAnalysis Create(Guid plotId, SoilAnalysisDraft analysis)
    {
        using var db = Open();
        using var tx = db.BeginTransaction();
        Exists(db, tx, "plots", plotId);
        References(db, tx, analysis);
        var id = Guid.NewGuid();
        using (var insert = new NpgsqlCommand("""
            INSERT INTO soil_analyses(id,plot_id,campaign_id,sampled_on,analyzed_on,laboratory_id,sampling_method,sampling_detail,map_link,notes)
            VALUES (@id,@plot,@campaign,@sampledOn,@analyzedOn,@laboratory,@method,@detail,@mapLink,@notes)
            """, db, tx))
        {
            insert.Parameters.AddWithValue("id", id);
            insert.Parameters.AddWithValue("plot", plotId);
            Bind(insert, analysis);
            insert.ExecuteNonQuery();
        }
        WriteResults(db, tx, id, analysis);
        var created = Read(db, tx, "a.id=@id", Uuid("id", id)).Single();
        tx.Commit();
        return created;
    }

    public SoilAnalysis Update(Guid id, long revision, SoilAnalysisDraft analysis)
    {
        using var db = Open();
        using var tx = db.BeginTransaction();
        using (var current = new NpgsqlCommand("SELECT revision FROM soil_analyses WHERE id=@id FOR UPDATE", db, tx))
        {
            current.Parameters.AddWithValue("id", id);
            var stored = current.ExecuteScalar() ?? throw new DomainNotFoundException();
            if ((long)stored != revision) throw new DomainConflictException();
        }
        References(db, tx, analysis);
        using (var update = new NpgsqlCommand("""
            UPDATE soil_analyses SET campaign_id=@campaign,sampled_on=@sampledOn,analyzed_on=@analyzedOn,laboratory_id=@laboratory,sampling_method=@method,
              sampling_detail=@detail,map_link=@mapLink,notes=@notes,revision=revision+1 WHERE id=@id;
            DELETE FROM soil_analysis_results WHERE analysis_id=@id;
            """, db, tx))
        {
            update.Parameters.AddWithValue("id", id);
            Bind(update, analysis);
            update.ExecuteNonQuery();
        }
        WriteResults(db, tx, id, analysis);
        var updated = Read(db, tx, "a.id=@id", Uuid("id", id)).Single();
        tx.Commit();
        return updated;
    }

    public SoilAnalysis? Get(Guid id)
    {
        using var db = Open();
        using var tx = db.BeginTransaction(System.Data.IsolationLevel.RepeatableRead);
        var found = Read(db, tx, "a.id=@id", Uuid("id", id)).SingleOrDefault();
        tx.Commit();
        return found;
    }

    public IReadOnlyList<SoilAnalysis> List(Guid? plotId)
    {
        using var db = Open();
        // One snapshot, so the results match the analyses they are read with.
        using var tx = db.BeginTransaction(System.Data.IsolationLevel.RepeatableRead);
        var analyses = Read(db, tx, "(@plot IS NULL OR a.plot_id=@plot)", Uuid("plot", plotId));
        tx.Commit();
        return analyses;
    }

    private static void Bind(NpgsqlCommand cmd, SoilAnalysisDraft analysis)
    {
        cmd.Parameters.AddWithValue("campaign", analysis.CampaignId!.Value);
        cmd.Parameters.Add(Day("sampledOn", analysis.SampledOn));
        cmd.Parameters.Add(Day("analyzedOn", analysis.AnalyzedOn));
        cmd.Parameters.Add(Uuid("laboratory", analysis.LaboratoryId));
        cmd.Parameters.AddWithValue("method", analysis.SamplingMethod!);
        cmd.Parameters.Add(Varchar("detail", analysis.SamplingDetail));
        cmd.Parameters.Add(Varchar("mapLink", analysis.MapLink));
        cmd.Parameters.Add(Varchar("notes", analysis.Notes));
    }

    private static void Exists(NpgsqlConnection db, NpgsqlTransaction tx, string table, Guid id)
    {
        using var exists = new NpgsqlCommand($"SELECT EXISTS(SELECT 1 FROM {table} WHERE id=@id)", db, tx);
        exists.Parameters.AddWithValue("id", id);
        if (exists.ExecuteScalar() is not true) throw new DomainNotFoundException();
    }

    /// <summary>A missing campaign or laboratory is not found; a provider that is not a soil laboratory is rejected.</summary>
    private static void References(NpgsqlConnection db, NpgsqlTransaction tx, SoilAnalysisDraft analysis)
    {
        Exists(db, tx, "campaigns", analysis.CampaignId!.Value);
        if (analysis.LaboratoryId is not { } laboratoryId) return;
        using var kind = new NpgsqlCommand("SELECT kind FROM providers WHERE id=@id FOR SHARE", db, tx);
        kind.Parameters.AddWithValue("id", laboratoryId);
        var found = kind.ExecuteScalar() ?? throw new DomainNotFoundException();
        if ((string)found != SoilAnalysisService.SoilLaboratory) throw new DomainValidationException("Laboratory must be a soil laboratory");
    }

    private static void WriteResults(NpgsqlConnection db, NpgsqlTransaction tx, Guid analysisId, SoilAnalysisDraft analysis)
    {
        for (var i = 0; i < analysis.Results!.Count; i++)
        {
            var result = analysis.Results[i];
            using var insert = new NpgsqlCommand("""
                INSERT INTO soil_analysis_results(id,analysis_id,position,parameter,value,unit,depth,station)
                VALUES (@id,@analysis,@position,@parameter,@value,@unit,@depth,@station)
                """, db, tx);
            insert.Parameters.AddWithValue("id", Guid.NewGuid());
            insert.Parameters.AddWithValue("analysis", analysisId);
            insert.Parameters.AddWithValue("position", (short)(i + 1));
            insert.Parameters.AddWithValue("parameter", result.Parameter!);
            insert.Parameters.AddWithValue("value", result.Value!.Value);
            insert.Parameters.Add(Varchar("unit", result.Unit));
            insert.Parameters.Add(Varchar("depth", result.Depth));
            insert.Parameters.Add(Varchar("station", result.Station));
            insert.ExecuteNonQuery();
        }
    }

    // Newest sampling first, then the newest analysis date (an analysis without one after those that have it), then newest created.
    private static List<SoilAnalysis> Read(NpgsqlConnection db, NpgsqlTransaction tx, string filter, NpgsqlParameter parameter)
    {
        var results = new Dictionary<Guid, List<SoilAnalysisResult>>();
        using (var cmd = new NpgsqlCommand($"""
            SELECT r.analysis_id,r.id,r.position,r.parameter,r.value,r.unit,r.depth,r.station
            FROM soil_analysis_results r JOIN soil_analyses a ON a.id=r.analysis_id WHERE {filter} ORDER BY r.analysis_id,r.position
            """, db, tx))
        {
            cmd.Parameters.Add(parameter.Clone());
            using var row = cmd.ExecuteReader();
            while (row.Read())
            {
                if (!results.TryGetValue(row.GetGuid(0), out var analysisResults)) results[row.GetGuid(0)] = analysisResults = [];
                analysisResults.Add(new(row.GetGuid(1), row.GetInt16(2), row.GetString(3), row.GetDecimal(4), Text(row, 5), Text(row, 6), Text(row, 7)));
            }
        }
        using var header = new NpgsqlCommand($"""
            SELECT {AnalysisColumns} FROM soil_analyses a WHERE {filter}
            ORDER BY a.sampled_on DESC,a.analyzed_on DESC NULLS LAST,a.created_at DESC,a.id
            """, db, tx);
        header.Parameters.Add(parameter.Clone());
        using var r = header.ExecuteReader();
        var analyses = new List<SoilAnalysis>();
        while (r.Read())
            analyses.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetFieldValue<DateOnly>(3), r.IsDBNull(4) ? null : r.GetFieldValue<DateOnly>(4), Id(r, 5),
                r.GetString(6), Text(r, 7), Text(r, 8), Text(r, 9), results.TryGetValue(r.GetGuid(0), out var analysisResults) ? analysisResults : [], r.GetInt64(10)));
        return analyses;
    }
}
