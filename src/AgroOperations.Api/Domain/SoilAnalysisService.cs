namespace AgroOperations.Api.Domain;

/// <summary>One measured parameter; unitless parameters such as pH have no unit.</summary>
public sealed record SoilAnalysisResult(Guid Id, short Position, string Parameter, decimal Value, string? Unit, string? Depth, string? Station);
public sealed record SoilAnalysis(Guid Id, Guid PlotId, Guid CampaignId, DateOnly SampledOn, DateOnly? AnalyzedOn, Guid? LaboratoryId, string SamplingMethod,
    string? SamplingDetail, string? MapLink, string? Notes, IReadOnlyList<SoilAnalysisResult> Results, long Revision);

public sealed record SoilResultDraft(string? Parameter, decimal? Value, string? Unit, string? Depth, string? Station);
/// <summary>Writes replace the analysis and its results. The plot is given on create only: an analysis never changes plot.</summary>
public sealed record SoilAnalysisDraft(Guid? CampaignId, DateOnly? SampledOn, DateOnly? AnalyzedOn, Guid? LaboratoryId, string? SamplingMethod, string? SamplingDetail,
    string? MapLink, string? Notes, IReadOnlyList<SoilResultDraft>? Results);

public interface ISoilAnalysisRepository
{
    /// <summary>A missing plot, campaign or laboratory is not found; a laboratory of another kind is rejected.</summary>
    SoilAnalysis Create(Guid plotId, SoilAnalysisDraft analysis);
    SoilAnalysis Update(Guid id, long revision, SoilAnalysisDraft analysis);
    SoilAnalysis? Get(Guid id);
    IReadOnlyList<SoilAnalysis> List(Guid? plotId);
}

/// <summary>
/// A soil analysis of a plot in a campaign (reference point 9), sampled over the plot average or at sampling stations, with
/// an optional link to the shape or KML file of the sampled area. The plot keeps every campaign's analyses.
/// </summary>
public sealed class SoilAnalysisService(ISoilAnalysisRepository repository)
{
    public const string SoilLaboratory = "laboratorio_suelos";

    private static string? Limited(string? value, int length, string field) => CatalogText.Limited(value, length, field);

    private static SoilAnalysisDraft Validated(SoilAnalysisDraft draft)
    {
        if (draft.CampaignId is null) throw new DomainValidationException("Campaign is required");
        if (draft.SampledOn is null) throw new DomainValidationException("Sampled on is required");
        var method = CatalogText.Optional(draft.SamplingMethod)?.ToLowerInvariant();
        if (method is not ("promedio" or "estaciones")) throw new DomainValidationException("Sampling method must be promedio or estaciones");
        var mapLink = CatalogText.Optional(draft.MapLink);
        if (mapLink is not null && (mapLink.Length > 300 || mapLink.Any(char.IsWhiteSpace)))
            throw new DomainValidationException("Map link must be a single address without spaces");
        if (draft.Results is not { Count: > 0 }) throw new DomainValidationException("At least one result is required");
        var results = draft.Results.Select(result =>
        {
            var parameter = Limited(CatalogText.Required(result.Parameter, "Parameter"), 100, "Parameter");
            if (result.Value is not { } value) throw new DomainValidationException("Value is required");
            if (!CatalogText.FitsAmount(Math.Abs(value))) throw new DomainValidationException("Value must fit numeric(14,4)");
            return new SoilResultDraft(parameter, value, Limited(result.Unit, 30, "Unit"), Limited(result.Depth, 30, "Depth"), Limited(result.Station, 60, "Station"));
        }).ToArray();
        if (method == "estaciones" && results.Any(result => result.Station is null))
            throw new DomainValidationException("Each result needs a station when sampling by stations");
        return draft with { SamplingMethod = method, SamplingDetail = CatalogText.Optional(draft.SamplingDetail), MapLink = mapLink,
            Notes = CatalogText.Optional(draft.Notes), Results = results };
    }

    public SoilAnalysis Create(Guid plotId, SoilAnalysisDraft analysis) => repository.Create(plotId, Validated(analysis));
    public SoilAnalysis Update(Guid id, long revision, SoilAnalysisDraft analysis) => repository.Update(id, revision, Validated(analysis));
    public SoilAnalysis? Get(Guid id) => repository.Get(id);
    /// <summary>Every campaign, newest sampling first, then newest analysis date; without a plot, every analysis.</summary>
    public IReadOnlyList<SoilAnalysis> List(Guid? plotId = null) => repository.List(plotId);
}
