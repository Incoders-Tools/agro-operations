using AgroOperations.Api.Domain;
using AgroOperations.Api.Persistence;
using System.Security.Claims;
using Npgsql;

namespace AgroOperations.Api.Backoffice;

/// <summary>Development-only local routes; production identity and authorization are separate work.</summary>
public static class BackofficeRoutes
{
    public static void Map(RouteGroupBuilder local, string connectionString)
    {
        var database = new NpgsqlConnectionStringBuilder(connectionString);
        if (database.Host is not ("127.0.0.1" or "localhost"))
            throw new InvalidOperationException("Local backoffice requires a loopback-only PostgreSQL host");
        // Refuse to advertise a healthy private API if the persisted database rejects these credentials.
        using (var probe = new NpgsqlConnection(connectionString)) probe.Open();
        var operations = new OperationsService(new PostgresOperationsRepository(connectionString));
        var crops = new CropCatalog(new PostgresCropRepository(connectionString));
        var campaigns = new CampaignCatalog(new PostgresCampaignRepository(connectionString));
        var laborTypes = new LaborTypeCatalog(new PostgresLaborTypeRepository(connectionString));
        var plotAreas = new PlotCampaignAreaService(new PostgresPlotCampaignAreaRepository(connectionString));
        var recipes = new RecipeService(new PostgresRecipeRepository(connectionString));
        var stages = new StageCatalog(new PostgresStageRepository(connectionString));
        var contractors = new ContractorCatalog(new PostgresContractorRepository(connectionString));
        var inputs = new InputCatalog(new PostgresInputRepository(connectionString));
        var firms = new CommercialFirmCatalog(new PostgresCommercialFirmRepository(connectionString));
        var plotCrops = new PlotCropService(new PostgresPlotCropRepository(connectionString));
        var provenance = new ImportProvenanceService(new PostgresImportRecordRepository(connectionString));
        var workOrders = new WorkOrderService(new PostgresWorkOrderRepository(connectionString));
        var protectedRoutes = local.MapGroup("").RequireAuthorization().AddEndpointFilter(async (context, next) =>
        {
            if (HttpMethods.IsGet(context.HttpContext.Request.Method)) return await next(context);
            var token = context.HttpContext.Request.Headers["X-Agro-CSRF"].ToString();
            var claim = context.HttpContext.User.FindFirstValue("csrf");
            return string.IsNullOrEmpty(token) || token != claim
                ? Results.StatusCode(403) : await next(context);
        });
        protectedRoutes.MapGet("/clients", () => Results.Json(operations.ListClients()));
        // Optional parent filters navigate client -> fields -> plots; without one, every row.
        protectedRoutes.MapGet("/fields", (Guid? clientId) => Results.Json(operations.ListFields(clientId)));
        protectedRoutes.MapGet("/plots", (Guid? fieldId) => Results.Json(operations.ListPlots(fieldId)));
        protectedRoutes.MapGet("/commercial-firms", (Guid? clientId) => Results.Json(firms.List(clientId)));
        protectedRoutes.MapGet("/plot-crops", (Guid? plotId) => Results.Json(plotCrops.List(plotId)));
        protectedRoutes.MapGet("/crops", () => Results.Json(crops.List()));
        protectedRoutes.MapGet("/campaigns", () => Results.Json(campaigns.List()));
        protectedRoutes.MapGet("/labor-types", () => Results.Json(laborTypes.List()));
        protectedRoutes.MapGet("/stages", () => Results.Json(stages.List()));
        protectedRoutes.MapGet("/contractors", () => Results.Json(contractors.List()));
        protectedRoutes.MapGet("/inputs", () => Results.Json(inputs.List()));
        protectedRoutes.MapGet("/plot-campaign-areas", () => Results.Json(plotAreas.List()));
        protectedRoutes.MapGet("/recipes", () => Results.Json(recipes.List()));
        protectedRoutes.MapGet("/recipe-lines", () => Results.Json(recipes.ListLines()));
        protectedRoutes.MapGet("/import-records", () => Results.Json(provenance.List()));
        // Work orders: optional firm, status and search (q) filters; an unknown status is rejected rather than matching nothing.
        protectedRoutes.MapGet("/work-orders", (Guid? firmId, string? status, string? q) => Change(() => workOrders.List(firmId, status, q)));
        protectedRoutes.MapGet("/work-orders/{id:guid}", (Guid id) => Item(workOrders.Get(id)));
        // The print view of one order: display names resolved here and the default PDF file name.
        protectedRoutes.MapGet("/work-orders/{id:guid}/document", (Guid id) => Item(workOrders.Document(id)));
        // The server takes the number from the firm; a client-sent number or firm on update is ignored, and so is a fieldId
        // from bodies written before an order could cover several fields.
        protectedRoutes.MapPost("/work-orders", (WorkOrderInput input) => Create(() => workOrders.Create(input.FirmId, input.Draft())));
        protectedRoutes.MapPut("/work-orders/{id:guid}", (Guid id, RevisionWorkOrderInput input) => Change(() => workOrders.Update(id, input.Revision, input.Draft())));
        protectedRoutes.MapPost("/work-orders/{id:guid}/cancel", (Guid id, CancelWorkOrderInput input) => Change(() => workOrders.Cancel(id, input.Revision, input.Reason)));
        protectedRoutes.MapPost("/work-orders/{id:guid}/complete", (Guid id, RevisionOnlyInput input) => Change(() => workOrders.Complete(id, input.Revision)));
        protectedRoutes.MapGet("/clients/{id:guid}", (Guid id) => Item(operations.GetClient(id)));
        protectedRoutes.MapGet("/fields/{id:guid}", (Guid id) => Item(operations.GetField(id)));
        protectedRoutes.MapGet("/plots/{id:guid}", (Guid id) => Item(operations.GetPlot(id)));
        // Client, field and plot attributes are optional, so the older name-only payloads keep working.
        protectedRoutes.MapPost("/clients", (ClientInput input) => Create(() => operations.CreateClient(input.Draft())));
        protectedRoutes.MapPost("/fields", (FieldInput input) => Create(() => operations.CreateField(input.ClientId, input.Draft())));
        protectedRoutes.MapPost("/plots", (PlotInput input) => Create(() => operations.CreatePlot(input.FieldId, input.Draft())));
        protectedRoutes.MapPost("/commercial-firms", (FirmInput input) => Create(() => firms.Create(input.ClientId, input.Name, input.TaxId)));
        protectedRoutes.MapPost("/plot-crops", (PlotCropInput input) =>
            Create(() => plotCrops.Create(input.PlotId, input.CampaignId, input.CropId, input.Position, input.Role, input.Variety,
                input.GrainYieldKgHa, input.ForageBiomassKgHa)));
        protectedRoutes.MapPost("/crops", (NameInput input) => Create(() => crops.Create(input.Name)));
        protectedRoutes.MapPost("/campaigns", (NameInput input) => Create(() => campaigns.Create(input.Name)));
        protectedRoutes.MapPost("/labor-types", (LaborInput input) =>
            Create(() => laborTypes.Create(input.Name, input.Abbreviation, input.Category, input.Description)));
        protectedRoutes.MapPost("/stages", (StageInput input) => Create(() => stages.Create(input.Name, input.SortOrder)));
        protectedRoutes.MapPost("/contractors", (ContractorInput input) => Create(() => contractors.Create(input.Draft())));
        protectedRoutes.MapPost("/inputs", (InputInput input) => Create(() => inputs.Create(input.Draft())));
        protectedRoutes.MapPost("/plot-campaign-areas", (PlotCampaignAreaInput input) =>
            Create(() => plotAreas.Create(input.PlotId, input.CampaignId, input.AreaHectares)));
        protectedRoutes.MapPost("/recipes", (RecipeInput input) =>
            Create(() => recipes.Create(input.PlotId, input.CampaignId, input.LaborTypeId, input.Stage, input.TargetUse, input.ResponsibleEngineer)));
        protectedRoutes.MapPost("/recipe-lines", (RecipeLineInput input) =>
            Create(() => recipes.CreateLine(input.RecipeId, input.ProductName, input.DoseValue, input.DoseUnit)));
        // PUT is a full replace: an optional attribute omitted from the body is stored as absent (null). Exception: a field's
        // clientId and a plot's fieldId move it when sent and keep the current parent when omitted.
        protectedRoutes.MapPut("/clients/{id:guid}", (Guid id, RevisionClientInput input) => Change(() => operations.UpdateClient(id, input.Revision, input.Draft())));
        protectedRoutes.MapPut("/fields/{id:guid}", (Guid id, RevisionFieldInput input) => Change(() => operations.UpdateField(id, input.Revision, input.Draft())));
        protectedRoutes.MapPut("/plots/{id:guid}", (Guid id, RevisionPlotInput input) => Change(() => operations.UpdatePlot(id, input.Revision, input.Draft())));
        // A firm keeps its client, and a plot crop keeps its plot, campaign and position: the bodies carry none of them.
        // Exception to the full-replace rule: an omitted nextWorkOrderNumber keeps the stored one, so a rename never renumbers.
        protectedRoutes.MapPut("/commercial-firms/{id:guid}", (Guid id, RevisionFirmInput input) =>
            Change(() => firms.Update(id, input.Revision, input.Name, input.TaxId, input.NextWorkOrderNumber)));
        protectedRoutes.MapPut("/plot-crops/{id:guid}", (Guid id, RevisionPlotCropInput input) =>
            Change(() => plotCrops.Update(id, input.Revision, input.CropId, input.Role, input.Variety, input.GrainYieldKgHa, input.ForageBiomassKgHa)));
        protectedRoutes.MapPut("/crops/{id:guid}", (Guid id, RevisionNameInput input) => Change(() => crops.Update(id, input.Revision, input.Name)));
        protectedRoutes.MapPut("/campaigns/{id:guid}", (Guid id, RevisionNameInput input) => Change(() => campaigns.Update(id, input.Revision, input.Name)));
        protectedRoutes.MapPut("/labor-types/{id:guid}", (Guid id, RevisionLaborInput input) =>
            Change(() => laborTypes.Update(id, input.Revision, input.Name, input.Abbreviation, input.Category, input.Description)));
        protectedRoutes.MapPut("/stages/{id:guid}", (Guid id, RevisionStageInput input) => Change(() => stages.Update(id, input.Revision, input.Name, input.SortOrder)));
        protectedRoutes.MapPut("/contractors/{id:guid}", (Guid id, RevisionContractorInput input) => Change(() => contractors.Update(id, input.Revision, input.Draft())));
        protectedRoutes.MapPut("/inputs/{id:guid}", (Guid id, RevisionInputInput input) => Change(() => inputs.Update(id, input.Revision, input.Draft())));
        protectedRoutes.MapPut("/plot-campaign-areas/{id:guid}", (Guid id, RevisionAreaInput input) =>
            Change(() => plotAreas.Update(id, input.Revision, input.AreaHectares)));
        protectedRoutes.MapPut("/recipes/{id:guid}", (Guid id, RevisionRecipeInput input) =>
            Change(() => recipes.Update(id, input.Revision, input.Stage, input.TargetUse, input.ResponsibleEngineer)));
        protectedRoutes.MapPut("/recipe-lines/{id:guid}", (Guid id, RevisionRecipeLineInput input) =>
            Change(() => recipes.UpdateLine(id, input.Revision, input.ProductName, input.DoseValue, input.DoseUnit)));
    }

    private static IResult Item<T>(T? item) where T : class => item is null ? Results.NotFound() : Results.Json(item);
    private static IResult Create<T>(Func<T> action) => Run(action, 201);
    private static IResult Change<T>(Func<T> action) => Run(action, 200);
    private static IResult Run<T>(Func<T> action, int success)
    {
        try { return Results.Json(action(), statusCode: success); }
        catch (DomainValidationException error) { return Results.BadRequest(new { error = error.Message }); }
        catch (DomainNotFoundException) { return Results.NotFound(new { error = "Record not found" }); }
        catch (DomainConflictException) { return Results.Conflict(new { error = "Stale revision" }); }
        catch (DomainDuplicateException error) { return Results.Conflict(new { error = error.Message }); }
        catch (DomainStateException error) { return Results.Conflict(new { error = error.Message }); }
    }
    public sealed record NameInput(string Name);
    public sealed record RevisionNameInput(string Name, long Revision);
    public sealed record ClientInput(string Name, string? TaxId, string? Producers, string? Email, string? Phone, string? Address)
    {
        public ClientDraft Draft() => new(Name, TaxId, Producers, Email, Phone, Address);
    }
    public sealed record RevisionClientInput(string Name, string? TaxId, string? Producers, string? Email, string? Phone, string? Address, long Revision)
    {
        public ClientDraft Draft() => new(Name, TaxId, Producers, Email, Phone, Address);
    }
    public sealed record FieldInput(Guid ClientId, string Name, string? Tenure, string? Locality)
    {
        public FieldDraft Draft() => new(Name, Tenure, Locality);
    }
    public sealed record RevisionFieldInput(string Name, string? Tenure, string? Locality, long Revision, Guid? ClientId)
    {
        public FieldDraft Draft() => new(Name, Tenure, Locality, ClientId);
    }
    public sealed record PlotInput(Guid FieldId, string Name, decimal AreaHectares, string? Code)
    {
        public PlotDraft Draft() => new(Name, AreaHectares, Code);
    }
    public sealed record RevisionPlotInput(string Name, decimal AreaHectares, string? Code, long Revision, Guid? FieldId)
    {
        public PlotDraft Draft() => new(Name, AreaHectares, Code, FieldId);
    }
    public sealed record FirmInput(Guid ClientId, string Name, string? TaxId);
    public sealed record RevisionFirmInput(string Name, string? TaxId, long Revision, int? NextWorkOrderNumber);
    public sealed record WorkOrderInput(Guid FirmId, DateOnly? IssuedOn, Guid LaborTypeId, Guid? StageId, Guid ContractorId,
        Guid? CampaignId, Guid? CropId, string? Variety, string? Observations, string? ResponsibleEngineer,
        WorkOrderPlotDraft[]? Plots, WorkOrderLineDraft[]? Lines)
    {
        public WorkOrderDraft Draft() => new(IssuedOn, LaborTypeId, StageId, ContractorId, CampaignId, CropId, Variety, Observations,
            ResponsibleEngineer, Plots, Lines);
    }
    public sealed record RevisionWorkOrderInput(DateOnly? IssuedOn, Guid LaborTypeId, Guid? StageId, Guid ContractorId,
        Guid? CampaignId, Guid? CropId, string? Variety, string? Observations, string? ResponsibleEngineer,
        WorkOrderPlotDraft[]? Plots, WorkOrderLineDraft[]? Lines, long Revision)
    {
        public WorkOrderDraft Draft() => new(IssuedOn, LaborTypeId, StageId, ContractorId, CampaignId, CropId, Variety, Observations,
            ResponsibleEngineer, Plots, Lines);
    }
    public sealed record CancelWorkOrderInput(long Revision, string? Reason);
    public sealed record RevisionOnlyInput(long Revision);
    public sealed record PlotCropInput(Guid PlotId, Guid CampaignId, Guid CropId, short Position, string Role, string? Variety,
        decimal? GrainYieldKgHa, decimal? ForageBiomassKgHa);
    public sealed record RevisionPlotCropInput(Guid CropId, string Role, string? Variety, long Revision, decimal? GrainYieldKgHa, decimal? ForageBiomassKgHa);
    public sealed record PlotCampaignAreaInput(Guid PlotId, Guid CampaignId, decimal AreaHectares);
    public sealed record RevisionAreaInput(decimal AreaHectares, long Revision);
    public sealed record RecipeInput(Guid PlotId, Guid CampaignId, Guid LaborTypeId, string Stage, string TargetUse, string ResponsibleEngineer);
    public sealed record RevisionRecipeInput(string Stage, string TargetUse, string ResponsibleEngineer, long Revision);
    public sealed record RecipeLineInput(Guid RecipeId, string ProductName, decimal DoseValue, string DoseUnit);
    public sealed record RevisionRecipeLineInput(string ProductName, decimal DoseValue, string DoseUnit, long Revision);
    public sealed record LaborInput(string Name, string? Abbreviation, string? Category, string? Description);
    public sealed record RevisionLaborInput(string Name, string? Abbreviation, long Revision, string? Category, string? Description);
    public sealed record StageInput(string Name, int SortOrder);
    public sealed record RevisionStageInput(string Name, int SortOrder, long Revision);
    public sealed record ContractorInput(string Name, string? TaxId, string? Phone, string? Email, string? Notes, bool OwnEquipment, string? Website)
    {
        public ContractorDraft Draft() => new(Name, TaxId, Phone, Email, Notes, OwnEquipment, Website);
    }
    public sealed record RevisionContractorInput(string Name, string? TaxId, string? Phone, string? Email, string? Notes, bool OwnEquipment, long Revision, string? Website)
    {
        public ContractorDraft Draft() => new(Name, TaxId, Phone, Email, Notes, OwnEquipment, Website);
    }
    public sealed record InputInput(string CommercialName, string? ActiveIngredient, string? Concentration, string? Formulation, short? MixOrder,
        string? Category, string Unit, decimal? DoseMin, decimal? DoseMax, decimal? ReferenceCost, bool HollowCone)
    {
        public InputDraft Draft() => new(CommercialName, ActiveIngredient, Concentration, Formulation, MixOrder, Category, Unit, DoseMin, DoseMax, ReferenceCost, HollowCone);
    }
    public sealed record RevisionInputInput(string CommercialName, string? ActiveIngredient, string? Concentration, string? Formulation, short? MixOrder,
        string? Category, string Unit, decimal? DoseMin, decimal? DoseMax, decimal? ReferenceCost, bool HollowCone, long Revision)
    {
        public InputDraft Draft() => new(CommercialName, ActiveIngredient, Concentration, Formulation, MixOrder, Category, Unit, DoseMin, DoseMax, ReferenceCost, HollowCone);
    }
}
