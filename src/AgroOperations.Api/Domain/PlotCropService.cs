namespace AgroOperations.Api.Domain;

public sealed record PlotCrop(Guid Id, Guid PlotId, Guid CampaignId, Guid CropId, short Position, string Role, string? Variety, long Revision);

public interface IPlotCropRepository
{
    PlotCrop Create(Guid plotId, Guid campaignId, Guid cropId, short position, string role, string? variety);
    PlotCrop Update(Guid id, long revision, Guid cropId, string role, string? variety);
    IReadOnlyList<PlotCrop> List(Guid? plotId);
}

/// <summary>
/// The crop history of a plot: up to two crops per campaign (positions 1 and 2). Renta or servicio is how
/// the crop is used on that plot in that campaign, so it lives here and not on the crop catalog.
/// Plot, campaign and position identify the record and never change.
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

    public PlotCrop Create(Guid plotId, Guid campaignId, Guid cropId, short position, string role, string? variety)
    {
        if (position is not (1 or 2)) throw new DomainValidationException("Position must be 1 or 2");
        return repository.Create(plotId, campaignId, cropId, position, Role(role), CatalogText.Optional(variety));
    }
    public PlotCrop Update(Guid id, long revision, Guid cropId, string role, string? variety) =>
        repository.Update(id, revision, cropId, Role(role), CatalogText.Optional(variety));
    /// <summary>Without a plot, every plot crop.</summary>
    public IReadOnlyList<PlotCrop> List(Guid? plotId = null) => repository.List(plotId);
}
