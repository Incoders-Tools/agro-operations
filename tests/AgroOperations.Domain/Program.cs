using AgroOperations.Api.Domain;
using AgroOperations.Api.Persistence;
using Npgsql;

var baseConnection = Environment.GetEnvironmentVariable("AGRO_TEST_DATABASE");
if (string.IsNullOrWhiteSpace(baseConnection))
{
    Console.Error.WriteLine("AGRO_TEST_DATABASE is required for isolated PostgreSQL integration tests");
    return 2;
}

var schema = "test_" + Guid.NewGuid().ToString("N");
var builder = new NpgsqlConnectionStringBuilder(baseConnection) { SearchPath = schema };
using var admin = new NpgsqlConnection(baseConnection);
admin.Open();
using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin)) create.ExecuteNonQuery();
try
{
    using (var connection = new NpgsqlConnection(builder.ConnectionString))
    {
        connection.Open();
        var migration = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/AgroOperations.Api/Persistence/Migrations/001_operations.sql"));
        using var command = new NpgsqlCommand(File.ReadAllText(migration), connection);
        command.ExecuteNonQuery();
        var cropsMigration = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/AgroOperations.Api/Persistence/Migrations/003_crops.sql"));
        using var cropsCommand = new NpgsqlCommand(File.ReadAllText(cropsMigration), connection);
        cropsCommand.ExecuteNonQuery();
        var agronomyMigration = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/AgroOperations.Api/Persistence/Migrations/004_agronomy.sql"));
        using var agronomyCommand = new NpgsqlCommand(File.ReadAllText(agronomyMigration), connection);
        agronomyCommand.ExecuteNonQuery();
    }
    var service = new OperationsService(new PostgresOperationsRepository(builder.ConnectionString));
    static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
    }
    static void Reject<T>(Action action, string label) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new Exception($"Expected {typeof(T).Name}: {label}");
    }
    Reject<DomainValidationException>(() => service.CreateClient(" \t"), "blank client");
    Check(service.GetClient(Guid.NewGuid()) is null, "unknown client");
    var client = service.CreateClient("Client");
    Reject<DomainValidationException>(() => service.CreateField(client.Id, " "), "blank field");
    Reject<DomainNotFoundException>(() => service.CreateField(Guid.NewGuid(), "Field"), "missing client");
    var field = service.CreateField(client.Id, "Field");
    Reject<DomainValidationException>(() => service.CreatePlot(field.Id, " ", 1), "blank plot");
    Reject<DomainNotFoundException>(() => service.CreatePlot(Guid.NewGuid(), "Plot", 1), "missing field");
    foreach (var area in new[] { 0m, -1m, 0.00001m, 100000000m })
        Reject<DomainValidationException>(() => service.CreatePlot(field.Id, "Plot", area), "invalid area");
    var plot = service.CreatePlot(field.Id, "Plot", 1.2500m);
    Check(service.ListClients().Any(item => item == client) &&
          service.ListFields().Any(item => item == field) &&
          service.ListPlots().Any(item => item == plot), "master record listings");
    Check(client.Id != Guid.Empty && field.Id != Guid.Empty && plot.Id != Guid.Empty &&
          field.ClientId == client.Id && plot.FieldId == field.Id && plot.AreaHectares == 1.2500m &&
          client.Revision == 1 && field.Revision == 1 && plot.Revision == 1, "initial hierarchy");
    Reject<DomainValidationException>(() => service.UpdateClient(client.Id, client.Revision, " "), "blank client update");
    Reject<DomainValidationException>(() => service.UpdateField(field.Id, field.Revision, " "), "blank field update");
    Reject<DomainValidationException>(() => service.UpdatePlot(plot.Id, plot.Revision, " ", 2), "blank plot update");
    foreach (var area in new[] { 0m, -1m, 0.00001m, 100000000m })
        Reject<DomainValidationException>(() => service.UpdatePlot(plot.Id, plot.Revision, "Changed", area), "invalid plot update");
    Check(service.GetClient(client.Id) == client && service.GetField(field.Id) == field && service.GetPlot(plot.Id) == plot, "rejected values unchanged");
    var updatedClient = service.UpdateClient(client.Id, client.Revision, "New client");
    var updatedField = service.UpdateField(field.Id, field.Revision, "New field");
    var updatedPlot = service.UpdatePlot(plot.Id, plot.Revision, "New plot", 2m);
    Reject<DomainConflictException>(() => service.UpdateClient(client.Id, client.Revision, "Stale"), "client conflict");
    Reject<DomainConflictException>(() => service.UpdateField(field.Id, field.Revision, "Stale"), "field conflict");
    Reject<DomainConflictException>(() => service.UpdatePlot(plot.Id, plot.Revision, "Stale", 3m), "plot conflict");
    Check(service.GetClient(client.Id) == updatedClient && service.GetField(field.Id) == updatedField &&
          service.GetPlot(plot.Id) == updatedPlot && updatedClient.Revision == 2 &&
          updatedField.Revision == 2 && updatedPlot.Revision == 2, "stale writes unchanged");
    Reject<DomainNotFoundException>(() => service.UpdatePlot(Guid.NewGuid(), 1, "Missing", 1m), "missing update");

    string Snapshot()
    {
        using var connection = new NpgsqlConnection(builder.ConnectionString);
        connection.Open();
        using var command = new NpgsqlCommand("""
            SELECT concat_ws('|',
              (SELECT coalesce(string_agg(to_jsonb(c)::text, ',' ORDER BY c.id), '') FROM clients c),
              (SELECT coalesce(string_agg(to_jsonb(f)::text, ',' ORDER BY f.id), '') FROM fields f),
              (SELECT coalesce(string_agg(to_jsonb(p)::text, ',' ORDER BY p.id), '') FROM plots p),
              (SELECT coalesce(string_agg(to_jsonb(c)::text, ',' ORDER BY c.id), '') FROM crops c),
              (SELECT coalesce(string_agg(to_jsonb(c)::text, ',' ORDER BY c.id), '') FROM campaigns c),
              (SELECT coalesce(string_agg(to_jsonb(a)::text, ',' ORDER BY a.id), '') FROM plot_campaign_areas a),
              (SELECT coalesce(string_agg(to_jsonb(l)::text, ',' ORDER BY l.id), '') FROM labor_types l),
              (SELECT coalesce(string_agg(to_jsonb(r)::text, ',' ORDER BY r.id), '') FROM recipes r),
              (SELECT coalesce(string_agg(to_jsonb(rl)::text, ',' ORDER BY rl.id), '') FROM recipe_lines rl))
            """, connection);
        return (string)command.ExecuteScalar()!;
    }
    var before = Snapshot();
    Reject<DomainNotFoundException>(() => service.CreatePlot(Guid.NewGuid(), "Orphan", 1m), "orphan plot");
    Check(Snapshot() == before, "orphan creation changed stored rows");
    Reject<DomainValidationException>(() => service.UpdatePlot(plot.Id, updatedPlot.Revision, "Plot", 0.00001m), "invalid precision");
    Check(Snapshot() == before, "invalid precision changed stored rows");

    using var gate = new Barrier(3);
    Task<Plot?> Compete(string name) => Task.Run(() =>
    {
        gate.SignalAndWait();
        try { return service.UpdatePlot(plot.Id, updatedPlot.Revision, name, 3m); }
        catch (DomainConflictException error)
        {
            Check(error.Message == "Stale revision", "wrong conflict message");
            return null;
        }
    });
    var first = Compete("First");
    var second = Compete("Second");
    gate.SignalAndWait();
    await Task.WhenAll(first, second);
    Check((first.Result is null) != (second.Result is null), "expected exactly one winner");
    var winner = first.Result ?? second.Result!;
    Check(winner.Revision == updatedPlot.Revision + 1 && service.GetPlot(plot.Id) == winner,
          "stale contender overwrote winner");
    var crops = new CropCatalog(new PostgresCropRepository(builder.ConnectionString));
    Reject<DomainValidationException>(() => crops.Create(" "), "blank crop");
    var crop = crops.Create("Trigo");
    Check(crop.Revision == 1 && crops.List().Any(item => item == crop), "crop creation/listing");
    var cropBefore = Snapshot();
    Reject<DomainValidationException>(() => crops.Create("  TRIGO  "), "duplicate crop");
    Reject<DomainValidationException>(() => crops.Create(" "), "blank crop");
    Check(Snapshot() == cropBefore, "rejected crop creates changed stored rows");
    var changedCrop = crops.Update(crop.Id, crop.Revision, "Trigo candeal");
    cropBefore = Snapshot();
    Reject<DomainConflictException>(() => crops.Update(crop.Id, crop.Revision, "Stale crop"), "crop conflict");
    Reject<DomainNotFoundException>(() => crops.Update(Guid.NewGuid(), 1, "Wheat"), "missing crop");
    Check(Snapshot() == cropBefore && crops.List().Single(item => item.Id == crop.Id) == changedCrop,
          "crop rejected writes changed stored rows");

    var campaigns = new CampaignCatalog(new PostgresCampaignRepository(builder.ConnectionString));
    Reject<DomainValidationException>(() => campaigns.Create(" "), "blank campaign");
    var campaign = campaigns.Create("2026-27");
    Check(campaign.Revision == 1 && campaigns.List().Any(item => item == campaign), "campaign creation/listing");
    var campaignBefore = Snapshot();
    Reject<DomainValidationException>(() => campaigns.Create("  2026-27  "), "duplicate campaign");
    Check(Snapshot() == campaignBefore, "rejected campaign create changed stored rows");

    var laborTypes = new LaborTypeCatalog(new PostgresLaborTypeRepository(builder.ConnectionString));
    Reject<DomainValidationException>(() => laborTypes.Create(" "), "blank labor type");
    var laborType = laborTypes.Create("Pulverización terrestre");
    Check(laborType.Revision == 1 && laborTypes.List().Any(item => item == laborType), "labor type creation/listing");

    var plotAreas = new PlotCampaignAreaService(new PostgresPlotCampaignAreaRepository(builder.ConnectionString));
    Reject<DomainNotFoundException>(() => plotAreas.Create(Guid.NewGuid(), campaign.Id, 1m), "area with missing plot");
    Reject<DomainNotFoundException>(() => plotAreas.Create(plot.Id, Guid.NewGuid(), 1m), "area with missing campaign");
    foreach (var invalidArea in new[] { 0m, -1m, 0.00001m, 100000000m })
        Reject<DomainValidationException>(() => plotAreas.Create(plot.Id, campaign.Id, invalidArea), "invalid campaign area");
    var plotArea = plotAreas.Create(plot.Id, campaign.Id, 2.75m);
    Check(plotArea.AreaHectares == 2.75m && plotAreas.List().Any(item => item == plotArea), "plot campaign area creation/listing");
    var plotAreaBefore = Snapshot();
    Reject<DomainValidationException>(() => plotAreas.Create(plot.Id, campaign.Id, 3m), "duplicate plot campaign area");
    Check(Snapshot() == plotAreaBefore, "rejected duplicate area changed stored rows");
    var updatedPlotArea = plotAreas.Update(plotArea.Id, plotArea.Revision, 3.1m);
    Reject<DomainConflictException>(() => plotAreas.Update(plotArea.Id, plotArea.Revision, 4m), "stale plot campaign area");
    Reject<DomainNotFoundException>(() => plotAreas.Update(Guid.NewGuid(), 1, 1m), "missing plot campaign area");
    Check(updatedPlotArea.AreaHectares == 3.1m && updatedPlotArea.Revision == 2, "plot campaign area update");

    var recipes = new RecipeService(new PostgresRecipeRepository(builder.ConnectionString));
    Reject<DomainNotFoundException>(() => recipes.Create(Guid.NewGuid(), campaign.Id, laborType.Id, "presiembra", "Barbecho", "Matías Kincaid"), "recipe with missing plot");
    Reject<DomainValidationException>(() => recipes.Create(plot.Id, campaign.Id, laborType.Id, "invalida", "Barbecho", "Matías Kincaid"), "recipe with invalid stage");
    Reject<DomainValidationException>(() => recipes.Create(plot.Id, campaign.Id, laborType.Id, "presiembra", " ", "Matías Kincaid"), "recipe with blank target use");
    var recipe = recipes.Create(plot.Id, campaign.Id, laborType.Id, "presiembra", "Barbecho", "Matías Kincaid");
    Check(recipe.Stage == "presiembra" && recipe.Revision == 1 && recipes.List().Any(item => item == recipe), "recipe creation/listing");
    var recipeBefore = Snapshot();
    Reject<DomainValidationException>(() => recipes.CreateLine(recipe.Id, " ", 2m, "l/ha"), "recipe line with blank product");
    foreach (var invalidDose in new[] { 0m, -1m, 0.00001m, 100000000m })
        Reject<DomainValidationException>(() => recipes.CreateLine(recipe.Id, "Glifosato equivalente ácido", invalidDose, "l/ha"), "invalid recipe line dose");
    Check(Snapshot() == recipeBefore, "rejected recipe line creates changed stored rows");
    var recipeLine = recipes.CreateLine(recipe.Id, "Glifosato equivalente ácido", 2m, "l/ha");
    Check(recipeLine.Revision == 1 && recipes.ListLines().Any(item => item == recipeLine), "recipe line creation/listing");
    var updatedRecipe = recipes.Update(recipe.Id, recipe.Revision, "postemergencia", "Control de malezas", "Matías Kincaid");
    Reject<DomainConflictException>(() => recipes.Update(recipe.Id, recipe.Revision, "presiembra", "Stale", "Matías Kincaid"), "stale recipe");
    Reject<DomainNotFoundException>(() => recipes.Update(Guid.NewGuid(), 1, "presiembra", "Missing", "Matías Kincaid"), "missing recipe");
    Check(updatedRecipe.Stage == "postemergencia" && updatedRecipe.Revision == 2, "recipe update");
    var updatedLine = recipes.UpdateLine(recipeLine.Id, recipeLine.Revision, "Glifosato equivalente ácido", 2.5m, "l/ha");
    Reject<DomainConflictException>(() => recipes.UpdateLine(recipeLine.Id, recipeLine.Revision, "Stale", 1m, "l/ha"), "stale recipe line");
    Reject<DomainNotFoundException>(() => recipes.UpdateLine(Guid.NewGuid(), 1, "Missing", 1m, "l/ha"), "missing recipe line");
    Check(updatedLine.DoseValue == 2.5m && updatedLine.Revision == 2, "recipe line update");

    Console.WriteLine("PASS: isolated PostgreSQL service boundary, validation, hierarchy, crops, campaigns, labor types, plot campaign areas and recipes with concurrent revisions");
    return 0;
}
finally
{
    using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin);
    drop.ExecuteNonQuery();
}
