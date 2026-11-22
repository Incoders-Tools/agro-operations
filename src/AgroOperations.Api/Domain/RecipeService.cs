namespace AgroOperations.Api.Domain;

public sealed record Recipe(Guid Id, Guid PlotId, Guid CampaignId, Guid LaborTypeId, string Stage, string TargetUse, string ResponsibleEngineer, long Revision);
public sealed record RecipeLine(Guid Id, Guid RecipeId, string ProductName, decimal DoseValue, string DoseUnit, long Revision);

public interface IRecipeRepository
{
    Recipe Create(Guid plotId, Guid campaignId, Guid laborTypeId, string stage, string targetUse, string responsibleEngineer);
    Recipe Update(Guid id, long revision, string stage, string targetUse, string responsibleEngineer);
    IReadOnlyList<Recipe> List();
    RecipeLine CreateLine(Guid recipeId, string productName, decimal doseValue, string doseUnit);
    RecipeLine UpdateLine(Guid id, long revision, string productName, decimal doseValue, string doseUnit);
    IReadOnlyList<RecipeLine> ListLines();
}

/// <summary>
/// A recipe is an internal planned-treatment record for one plot and campaign.
/// It never represents an officially issued government prescription, and creating
/// or editing it never proves that the labor was executed in the field.
/// </summary>
public sealed class RecipeService(IRecipeRepository repository)
{
    private static readonly string[] Stages = ["presiembra", "preemergencia", "postemergencia"];

    private static string ValidatedStage(string stage) => Array.IndexOf(Stages, stage) >= 0
        ? stage : throw new DomainValidationException("Stage must be presiembra, preemergencia or postemergencia");
    private static string Required(string value, string field) => !string.IsNullOrWhiteSpace(value)
        ? value.Trim() : throw new DomainValidationException($"{field} is required");
    private static decimal ValidatedDose(decimal doseValue) =>
        doseValue > 0 && decimal.Round(doseValue, 4) == doseValue && doseValue <= 99999999.9999m
            ? doseValue : throw new DomainValidationException("Dose must be positive and fit numeric(12,4)");

    public Recipe Create(Guid plotId, Guid campaignId, Guid laborTypeId, string stage, string targetUse, string responsibleEngineer) =>
        repository.Create(plotId, campaignId, laborTypeId, ValidatedStage(stage),
            Required(targetUse, "Target use"), Required(responsibleEngineer, "Responsible engineer"));
    public Recipe Update(Guid id, long revision, string stage, string targetUse, string responsibleEngineer) =>
        repository.Update(id, revision, ValidatedStage(stage), Required(targetUse, "Target use"), Required(responsibleEngineer, "Responsible engineer"));
    public IReadOnlyList<Recipe> List() => repository.List();

    public RecipeLine CreateLine(Guid recipeId, string productName, decimal doseValue, string doseUnit) =>
        repository.CreateLine(recipeId, Required(productName, "Product name"), ValidatedDose(doseValue), Required(doseUnit, "Dose unit"));
    public RecipeLine UpdateLine(Guid id, long revision, string productName, decimal doseValue, string doseUnit) =>
        repository.UpdateLine(id, revision, Required(productName, "Product name"), ValidatedDose(doseValue), Required(doseUnit, "Dose unit"));
    public IReadOnlyList<RecipeLine> ListLines() => repository.ListLines();
}
