namespace AgroOperations.Api.Domain;

public sealed record InputProduct(Guid Id, string CommercialName, string? ActiveIngredient, string? Concentration, string? Formulation,
    short? MixOrder, string? Category, string Unit, decimal? DoseMin, decimal? DoseMax, decimal? ReferenceCost, bool HollowCone, long Revision);
public sealed record InputDraft(string CommercialName, string? ActiveIngredient, string? Concentration, string? Formulation,
    short? MixOrder, string? Category, string Unit, decimal? DoseMin, decimal? DoseMax, decimal? ReferenceCost, bool HollowCone);

public interface IInputRepository
{
    InputProduct Create(InputDraft input);
    InputProduct Update(Guid id, long revision, InputDraft input);
    IReadOnlyList<InputProduct> List();
}

/// <summary>
/// An input is a product applied in a labor (agrochemical, fertilizer, seed treatment...).
/// The dose range is a reference for warnings on work orders, never a value applied automatically.
/// </summary>
public sealed class InputCatalog(IInputRepository repository)
{
    private static readonly string[] Categories =
        ["fertilizante", "herbicida", "insecticida", "fungicida", "coadyuvante", "curasemilla", "bioestimulante", "terapico", "inoculante"];

    private static InputDraft Validated(InputDraft draft)
    {
        var category = CatalogText.Optional(draft.Category)?.ToLowerInvariant();
        if (category is not null && Array.IndexOf(Categories, category) < 0)
            throw new DomainValidationException($"Category must be one of: {string.Join(", ", Categories)}");
        if (draft.MixOrder is < 1 or > 20) throw new DomainValidationException("Mix order must be between 1 and 20");
        var doseMin = Dose(draft.DoseMin, "Minimum dose");
        var doseMax = Dose(draft.DoseMax, "Maximum dose");
        if (doseMin > doseMax) throw new DomainValidationException("Minimum dose must not exceed maximum dose");
        if (draft.ReferenceCost is { } cost && (cost < 0 || decimal.Round(cost, 4) != cost || cost > 9999999999.9999m))
            throw new DomainValidationException("Reference cost must be zero or positive and fit numeric(14,4)");
        return new(CatalogText.Required(draft.CommercialName, "Commercial name"), CatalogText.Optional(draft.ActiveIngredient),
            CatalogText.Optional(draft.Concentration), CatalogText.Optional(draft.Formulation), draft.MixOrder, category,
            CatalogText.Required(draft.Unit, "Unit"), doseMin, doseMax, draft.ReferenceCost, draft.HollowCone);
    }

    private static decimal? Dose(decimal? dose, string field) =>
        dose is not { } value || (value > 0 && decimal.Round(value, 4) == value && value <= 99999999.9999m)
            ? dose : throw new DomainValidationException($"{field} must be positive and fit numeric(12,4)");

    public InputProduct Create(InputDraft input) => repository.Create(Validated(input));
    public InputProduct Update(Guid id, long revision, InputDraft input) => repository.Update(id, revision, Validated(input));
    public IReadOnlyList<InputProduct> List() => repository.List();
}
