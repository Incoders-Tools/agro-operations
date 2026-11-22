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
        var operations = new OperationsService(new PostgresOperationsRepository(connectionString));
        var crops = new CropCatalog(new PostgresCropRepository(connectionString));
        var protectedRoutes = local.MapGroup("").RequireAuthorization().AddEndpointFilter(async (context, next) =>
        {
            if (HttpMethods.IsGet(context.HttpContext.Request.Method)) return await next(context);
            var token = context.HttpContext.Request.Headers["X-Agro-CSRF"].ToString();
            var claim = context.HttpContext.User.FindFirstValue("csrf");
            return string.IsNullOrEmpty(token) || token != claim
                ? Results.StatusCode(403) : await next(context);
        });
        protectedRoutes.MapGet("/clients", () => Results.Json(operations.ListClients()));
        protectedRoutes.MapGet("/fields", () => Results.Json(operations.ListFields()));
        protectedRoutes.MapGet("/plots", () => Results.Json(operations.ListPlots()));
        protectedRoutes.MapGet("/crops", () => Results.Json(crops.List()));
        protectedRoutes.MapGet("/clients/{id:guid}", (Guid id) => Item(operations.GetClient(id)));
        protectedRoutes.MapGet("/fields/{id:guid}", (Guid id) => Item(operations.GetField(id)));
        protectedRoutes.MapGet("/plots/{id:guid}", (Guid id) => Item(operations.GetPlot(id)));
        protectedRoutes.MapPost("/clients", (NameInput input) => Create(() => operations.CreateClient(input.Name)));
        protectedRoutes.MapPost("/fields", (FieldInput input) => Create(() => operations.CreateField(input.ClientId, input.Name)));
        protectedRoutes.MapPost("/plots", (PlotInput input) => Create(() => operations.CreatePlot(input.FieldId, input.Name, input.AreaHectares)));
        protectedRoutes.MapPost("/crops", (NameInput input) => Create(() => crops.Create(input.Name)));
        protectedRoutes.MapPut("/clients/{id:guid}", (Guid id, RevisionNameInput input) => Change(() => operations.UpdateClient(id, input.Revision, input.Name)));
        protectedRoutes.MapPut("/fields/{id:guid}", (Guid id, RevisionNameInput input) => Change(() => operations.UpdateField(id, input.Revision, input.Name)));
        protectedRoutes.MapPut("/plots/{id:guid}", (Guid id, RevisionPlotInput input) => Change(() => operations.UpdatePlot(id, input.Revision, input.Name, input.AreaHectares)));
        protectedRoutes.MapPut("/crops/{id:guid}", (Guid id, RevisionNameInput input) => Change(() => crops.Update(id, input.Revision, input.Name)));
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
    }
    public sealed record NameInput(string Name);
    public sealed record FieldInput(Guid ClientId, string Name);
    public sealed record PlotInput(Guid FieldId, string Name, decimal AreaHectares);
    public sealed record RevisionNameInput(string Name, long Revision);
    public sealed record RevisionPlotInput(string Name, decimal AreaHectares, long Revision);
}
