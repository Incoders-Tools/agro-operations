namespace AgroOperations.Api.Domain;

public sealed record PlotCrop(Guid Id, Guid PlotId, Guid CampaignId, Guid CropId, short Position, string Role, string? Variety,
    decimal? GrainYieldKgHa, decimal? ForageBiomassKgHa, long Revision);

public interface IPlotCropRepository
{
    PlotCrop Create(Guid plotId, Guid campaignId, Guid cropId, short position, string role, string? variety, decimal? grainYieldKgHa, decimal? forageBiomassKgHa);
    PlotCrop Update(Guid id, long revision, Guid cropId, string role, string? variety, decimal? grainYieldKgHa, decimal? forageBiomassKgHa);
    IReadOnlyList<PlotCrop> List(Guid? plotId);
}

/// <summary>
/// The crop history of a plot: up to two crops per campaign (positions 1 and 2). Renta or servicio is how
/// the crop is used on that plot in that campaign, so it lives here and not on the crop catalog.
/// Plot, campaign and position identify the record and never change. The optional yields are the average harvest
/// of grain and of forage biomass in kg/ha; an update replaces crop, role, variety and both yields.
/// </summary>
public sealed class PlotCropService(IPlotCropRepository repository)
{
    private static readonly string[] Roles = ["renta", "servicio"];

    private static string Role(string? value)
    {
        var role = CatalogText.Optional(value)?.ToLowerInvariant();
        return role is not null && Array.IndexOf(Roles, role) >= 0
            ? role : throw new DomainValidationException($"Role must be one of: {string.Join(", ", Roles)}");
    }

    private static decimal? Yield(decimal? value) =>
        value is not { } yield || (yield >= 0 && decimal.Round(yield, 2) == yield && yield <= 9999999999.99m)
            ? value : throw new DomainValidationException("Yield must be zero or positive and fit numeric(12,2)");

    public PlotCrop Create(Guid plotId, Guid campaignId, Guid cropId, short position, string role, string? variety,
        decimal? grainYieldKgHa = null, decimal? forageBiomassKgHa = null)
    {
        if (position is not (1 or 2)) throw new DomainValidationException("Position must be 1 or 2");
        return repository.Create(plotId, campaignId, cropId, position, Role(role), CatalogText.Optional(variety), Yield(grainYieldKgHa), Yield(forageBiomassKgHa));
    }
    public PlotCrop Update(Guid id, long revision, Guid cropId, string role, string? variety, decimal? grainYieldKgHa = null, decimal? forageBiomassKgHa = null) =>
        repository.Update(id, revision, cropId, Role(role), CatalogText.Optional(variety), Yield(grainYieldKgHa), Yield(forageBiomassKgHa));
    /// <summary>Without a plot, every plot crop.</summary>
    public IReadOnlyList<PlotCrop> List(Guid? plotId = null) => repository.List(plotId);
}
