namespace AgroOperations.Api.Domain;

public sealed record PlotCampaignArea(Guid Id, Guid PlotId, Guid CampaignId, decimal AreaHectares, long Revision);

public interface IPlotCampaignAreaRepository
{
    PlotCampaignArea Create(Guid plotId, Guid campaignId, decimal areaHectares);
    PlotCampaignArea Update(Guid id, long revision, decimal areaHectares);
    IReadOnlyList<PlotCampaignArea> List();
}

/// <summary>Records the worked area of a plot for one campaign; plots.area_hectares stays only a reference value.</summary>
public sealed class PlotCampaignAreaService(IPlotCampaignAreaRepository repository)
{
    private static decimal Validated(decimal areaHectares) =>
        areaHectares > 0 && decimal.Round(areaHectares, 4) == areaHectares && areaHectares <= 99999999.9999m
            ? areaHectares : throw new DomainValidationException("Area must be positive and fit numeric(12,4)");

    public PlotCampaignArea Create(Guid plotId, Guid campaignId, decimal areaHectares) =>
        repository.Create(plotId, campaignId, Validated(areaHectares));
    public PlotCampaignArea Update(Guid id, long revision, decimal areaHectares) =>
        repository.Update(id, revision, Validated(areaHectares));
    public IReadOnlyList<PlotCampaignArea> List() => repository.List();
}
