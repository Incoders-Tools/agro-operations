namespace AgroOperations.Api.Domain;

public sealed record SeedAnalysis(Guid Id, Guid ClientId, Guid CropId, string? Variety, Guid? CampaignId, DateOnly AnalyzedOn, Guid? LaboratoryId,
    decimal? ThousandSeedWeightG, decimal? GerminationTreatedPct, decimal? AbnormalTreatedPct, decimal? DeadTreatedPct, decimal? HardTreatedPct,
    decimal? GerminationUntreatedPct, decimal? AbnormalUntreatedPct, decimal? DeadUntreatedPct, decimal? HardUntreatedPct, decimal? VigorPct, string? Notes,
    long Revision);
/// <summary>Writes replace the analysis, including its client and crop.</summary>
public sealed record SeedAnalysisDraft(Guid? ClientId, Guid? CropId, string? Variety, Guid? CampaignId, DateOnly? AnalyzedOn, Guid? LaboratoryId,
    decimal? ThousandSeedWeightG, decimal? GerminationTreatedPct, decimal? AbnormalTreatedPct, decimal? DeadTreatedPct, decimal? HardTreatedPct,
    decimal? GerminationUntreatedPct, decimal? AbnormalUntreatedPct, decimal? DeadUntreatedPct, decimal? HardUntreatedPct, decimal? VigorPct, string? Notes);

public interface ISeedAnalysisRepository
{
    /// <summary>A missing client, crop, campaign or laboratory is not found; a laboratory of another kind is rejected.</summary>
    SeedAnalysis Create(SeedAnalysisDraft analysis);
    SeedAnalysis Update(Guid id, long revision, SeedAnalysisDraft analysis);
    SeedAnalysis? Get(Guid id);
    IReadOnlyList<SeedAnalysis> List(Guid? clientId, Guid? cropId);
}

/// <summary>
/// The seed quality of a client's crop and variety or hybrid (reference point 10). Every measured value is optional, since
/// the data is sometimes unknown; per seed treatment the germinated, abnormal, dead and hard fractions add up to at most 100.
/// </summary>
public sealed class SeedAnalysisService(ISeedAnalysisRepository repository)
{
    public const string SeedLaboratory = "laboratorio_semillas";

    private static void Percentages(params decimal?[] values)
    {
        foreach (var value in values.OfType<decimal>())
        {
            if (value is < 0 or > 100) throw new DomainValidationException("Percentages must be between 0 and 100");
            if (decimal.Round(value, 2) != value) throw new DomainValidationException("Percentages must have at most two decimals");
        }
    }

    private static void Fractions(params decimal?[] values)
    {
        if (values.Sum(value => value ?? 0) > 100) throw new DomainValidationException("Seed fractions add up to more than 100");
    }

    private static SeedAnalysisDraft Validated(SeedAnalysisDraft draft)
    {
        if (draft.ClientId is null) throw new DomainValidationException("Client is required");
        if (draft.CropId is null) throw new DomainValidationException("Crop is required");
        if (draft.AnalyzedOn is null) throw new DomainValidationException("Analyzed on is required");
        if (draft.ThousandSeedWeightG is { } weight && (weight <= 0 || decimal.Round(weight, 4) != weight || weight >= 1000000))
            throw new DomainValidationException("Thousand seed weight must be positive and fit numeric(10,4)");
        Percentages(draft.GerminationTreatedPct, draft.AbnormalTreatedPct, draft.DeadTreatedPct, draft.HardTreatedPct, draft.GerminationUntreatedPct,
            draft.AbnormalUntreatedPct, draft.DeadUntreatedPct, draft.HardUntreatedPct, draft.VigorPct);
        Fractions(draft.GerminationTreatedPct, draft.AbnormalTreatedPct, draft.DeadTreatedPct, draft.HardTreatedPct);
        Fractions(draft.GerminationUntreatedPct, draft.AbnormalUntreatedPct, draft.DeadUntreatedPct, draft.HardUntreatedPct);
        return draft with { Variety = CatalogText.Optional(draft.Variety), Notes = CatalogText.Optional(draft.Notes) };
    }

    public SeedAnalysis Create(SeedAnalysisDraft analysis) => repository.Create(Validated(analysis));
    public SeedAnalysis Update(Guid id, long revision, SeedAnalysisDraft analysis) => repository.Update(id, revision, Validated(analysis));
    public SeedAnalysis? Get(Guid id) => repository.Get(id);
    /// <summary>Newest analysis first; both filters are optional and combine.</summary>
    public IReadOnlyList<SeedAnalysis> List(Guid? clientId = null, Guid? cropId = null) => repository.List(clientId, cropId);
}
