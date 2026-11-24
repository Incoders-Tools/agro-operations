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
var seedSchema = "test_" + Guid.NewGuid().ToString("N");
var builder = new NpgsqlConnectionStringBuilder(baseConnection) { SearchPath = schema };
var seedBuilder = new NpgsqlConnectionStringBuilder(baseConnection) { SearchPath = seedSchema };
var migrations = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/AgroOperations.Api/Persistence/Migrations"));
using var admin = new NpgsqlConnection(baseConnection);
admin.Open();
using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"; CREATE SCHEMA \"{seedSchema}\"", admin)) create.ExecuteNonQuery();
try
{
    void Migrate(string connectionString, params string[] files)
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        foreach (var file in files)
        {
            using var command = new NpgsqlCommand(File.ReadAllText(Path.Combine(migrations, file)), connection);
            command.ExecuteNonQuery();
        }
    }
    string[] previousMigrations = ["001_operations.sql", "003_crops.sql", "004_agronomy.sql", "005_import_records.sql"];
    Migrate(builder.ConnectionString, [.. previousMigrations, "006_catalogs.sql", "007_attributes.sql", "008_work_orders.sql"]);
    var service = new OperationsService(new PostgresOperationsRepository(builder.ConnectionString));
    static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
    }
    static T Rejected<T>(Action action, string label) where T : Exception
    {
        try { action(); } catch (T error) { return error; }
        throw new Exception($"Expected {typeof(T).Name}: {label}");
    }
    static void Reject<T>(Action action, string label) where T : Exception => Rejected<T>(action, label);
    Reject<DomainValidationException>(() => service.CreateClient(new ClientDraft(" \t")), "blank client");
    Check(service.GetClient(Guid.NewGuid()) is null, "unknown client");
    var client = service.CreateClient(new ClientDraft("Client"));
    Reject<DomainValidationException>(() => service.CreateField(client.Id, new FieldDraft(" ")), "blank field");
    Reject<DomainNotFoundException>(() => service.CreateField(Guid.NewGuid(), new FieldDraft("Field")), "missing client");
    var field = service.CreateField(client.Id, new FieldDraft("Field"));
    Reject<DomainValidationException>(() => service.CreatePlot(field.Id, new PlotDraft(" ", 1)), "blank plot");
    Reject<DomainNotFoundException>(() => service.CreatePlot(Guid.NewGuid(), new PlotDraft("Plot", 1)), "missing field");
    foreach (var area in new[] { 0m, -1m, 0.00001m, 100000000m })
        Reject<DomainValidationException>(() => service.CreatePlot(field.Id, new PlotDraft("Plot", area)), "invalid area");
    var plot = service.CreatePlot(field.Id, new PlotDraft("Plot", 1.2500m));
    Check(client is { TaxId: null, Producers: null, Email: null, Phone: null, Address: null } &&
          field is { Tenure: null, Locality: null } && plot.Code is null, "records without attributes keep them absent");
    Check(service.ListClients().Any(item => item == client) &&
          service.ListFields().Any(item => item == field) &&
          service.ListPlots().Any(item => item == plot), "master record listings");
    Check(client.Id != Guid.Empty && field.Id != Guid.Empty && plot.Id != Guid.Empty &&
          field.ClientId == client.Id && plot.FieldId == field.Id && plot.AreaHectares == 1.2500m &&
          client.Revision == 1 && field.Revision == 1 && plot.Revision == 1, "initial hierarchy");
    Reject<DomainValidationException>(() => service.UpdateClient(client.Id, client.Revision, new ClientDraft(" ")), "blank client update");
    Reject<DomainValidationException>(() => service.UpdateField(field.Id, field.Revision, new FieldDraft(" ")), "blank field update");
    Reject<DomainValidationException>(() => service.UpdatePlot(plot.Id, plot.Revision, new PlotDraft(" ", 2)), "blank plot update");
    foreach (var area in new[] { 0m, -1m, 0.00001m, 100000000m })
        Reject<DomainValidationException>(() => service.UpdatePlot(plot.Id, plot.Revision, new PlotDraft("Changed", area)), "invalid plot update");
    Check(service.GetClient(client.Id) == client && service.GetField(field.Id) == field && service.GetPlot(plot.Id) == plot, "rejected values unchanged");
    var updatedClient = service.UpdateClient(client.Id, client.Revision, new ClientDraft("New client"));
    var updatedField = service.UpdateField(field.Id, field.Revision, new FieldDraft("New field"));
    var updatedPlot = service.UpdatePlot(plot.Id, plot.Revision, new PlotDraft("New plot", 2m));
    Reject<DomainConflictException>(() => service.UpdateClient(client.Id, client.Revision, new ClientDraft("Stale")), "client conflict");
    Reject<DomainConflictException>(() => service.UpdateField(field.Id, field.Revision, new FieldDraft("Stale")), "field conflict");
    Reject<DomainConflictException>(() => service.UpdatePlot(plot.Id, plot.Revision, new PlotDraft("Stale", 3m)), "plot conflict");
    Check(service.GetClient(client.Id) == updatedClient && service.GetField(field.Id) == updatedField &&
          service.GetPlot(plot.Id) == updatedPlot && updatedClient.Revision == 2 &&
          updatedField.Revision == 2 && updatedPlot.Revision == 2, "stale writes unchanged");
    Reject<DomainNotFoundException>(() => service.UpdatePlot(Guid.NewGuid(), 1, new PlotDraft("Missing", 1m)), "missing update");

    var keptCropId = Guid.NewGuid();
    string Snapshot() => SnapshotOf(builder.ConnectionString) + "|" + AttributeTablesOf(builder.ConnectionString);
    // Without attributes, the snapshot ignores the 007 columns and the 007 crop seed, so it compares
    // a schema before and after 007 by what earlier migrations stored.
    string SnapshotOf(string connectionString, bool withAttributes = true)
    {
        var strip = withAttributes ? "" : " - '{tax_id,producers,email,phone,address,tenure,locality,code}'::text[]";
        var cropFilter = withAttributes ? "" : $" WHERE c.id = '{keptCropId}'";
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand($$"""
            SELECT concat_ws('|',
              (SELECT coalesce(string_agg((to_jsonb(c){{strip}})::text, ',' ORDER BY c.id), '') FROM clients c),
              (SELECT coalesce(string_agg((to_jsonb(f){{strip}})::text, ',' ORDER BY f.id), '') FROM fields f),
              (SELECT coalesce(string_agg((to_jsonb(p){{strip}})::text, ',' ORDER BY p.id), '') FROM plots p),
              (SELECT coalesce(string_agg(to_jsonb(c)::text, ',' ORDER BY c.id), '') FROM crops c{{cropFilter}}),
              (SELECT coalesce(string_agg(to_jsonb(c)::text, ',' ORDER BY c.id), '') FROM campaigns c),
              (SELECT coalesce(string_agg(to_jsonb(a)::text, ',' ORDER BY a.id), '') FROM plot_campaign_areas a),
              (SELECT coalesce(string_agg(to_jsonb(l)::text, ',' ORDER BY l.id), '') FROM labor_types l),
              (SELECT coalesce(string_agg(to_jsonb(r)::text, ',' ORDER BY r.id), '') FROM recipes r),
              (SELECT coalesce(string_agg(to_jsonb(rl)::text, ',' ORDER BY rl.id), '') FROM recipe_lines rl),
              (SELECT coalesce(string_agg(to_jsonb(s)::text, ',' ORDER BY s.id), '') FROM stages s),
              (SELECT coalesce(string_agg(to_jsonb(c)::text, ',' ORDER BY c.id), '') FROM contractors c),
              (SELECT coalesce(string_agg(to_jsonb(i)::text, ',' ORDER BY i.id), '') FROM inputs i),
              (SELECT coalesce(string_agg(to_jsonb(ir)::text, ',' ORDER BY ir.id), '') FROM import_records ir))
            """, connection);
        return (string)command.ExecuteScalar()!;
    }
    // Without the next number, the snapshot compares commercial firms before and after 008 by what 007 stored.
    string AttributeTablesOf(string connectionString, bool withNextNumber = true)
    {
        var strip = withNextNumber ? "" : " - 'next_work_order_number'";
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand($"""
            SELECT concat_ws('|',
              (SELECT coalesce(string_agg((to_jsonb(f){strip})::text, ',' ORDER BY f.id), '') FROM commercial_firms f),
              (SELECT coalesce(string_agg(to_jsonb(pc)::text, ',' ORDER BY pc.id), '') FROM plot_crops pc))
            """, connection);
        return (string)command.ExecuteScalar()!;
    }
    var before = Snapshot();
    Reject<DomainNotFoundException>(() => service.CreatePlot(Guid.NewGuid(), new PlotDraft("Orphan", 1m)), "orphan plot");
    Check(Snapshot() == before, "orphan creation changed stored rows");
    Reject<DomainValidationException>(() => service.UpdatePlot(plot.Id, updatedPlot.Revision, new PlotDraft("Plot", 0.00001m)), "invalid precision");
    Check(Snapshot() == before, "invalid precision changed stored rows");

    using var gate = new Barrier(3);
    Task<Plot?> Compete(string name) => Task.Run(() =>
    {
        gate.SignalAndWait();
        try { return service.UpdatePlot(plot.Id, updatedPlot.Revision, new PlotDraft(name, 3m)); }
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
    string[] referenceCrops = ["Trigo", "Cebada", "Avena", "Garbanzo", "Lenteja", "Colza", "Camelina", "Carinata", "Arveja verde",
        "Arveja amarilla", "Centeno", "Triticale", "Vicia", "Nabo", "Pastura de gramíneas", "Pastura de leguminosas", "Pastura consociada",
        "Raigrás", "Soja", "Maíz", "Maíz pisingallo", "Sorgo granífero", "Sorgo forrajero", "Sorgo silero", "Maíz silo", "Girasol",
        "Poroto mung", "Moha"];
    var seededCrops = crops.List();
    Check(seededCrops.Count == referenceCrops.Length && referenceCrops.All(name => seededCrops.Any(item => item.Name == name && item.Revision == 1)),
          "seeded reference crops");
    Reject<DomainValidationException>(() => crops.Create(" "), "blank crop");
    var crop = crops.Create("Cultivo de prueba");
    Check(crop.Revision == 1 && crops.List().Any(item => item == crop), "crop creation/listing");
    var cropBefore = Snapshot();
    Reject<DomainDuplicateException>(() => crops.Create("  TRIGO  "), "duplicate seeded crop");
    Reject<DomainDuplicateException>(() => crops.Create("  CULTIVO DE PRUEBA  "), "duplicate crop");
    Reject<DomainValidationException>(() => crops.Create(" "), "blank crop");
    Check(Snapshot() == cropBefore, "rejected crop creates changed stored rows");
    var changedCrop = crops.Update(crop.Id, crop.Revision, "Cultivo de prueba candeal");
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
    Reject<DomainDuplicateException>(() => campaigns.Create("  2026-27  "), "duplicate campaign");
    Check(Snapshot() == campaignBefore, "rejected campaign create changed stored rows");

    var laborTypes = new LaborTypeCatalog(new PostgresLaborTypeRepository(builder.ConnectionString));
    (string Name, string Abbreviation)[] expectedLabors = [
        ("Cosecha fina", "COS-FIN"), ("Cosecha gruesa", "COS-GRU"), ("Disco", "DISCO"), ("Fertilización al voleo", "FERT-V"),
        ("Fertilización incorporada", "FERT-INC"), ("Fertilización líquida", "FERT-LIQ"), ("Pulverización aérea", "PULV-AE"),
        ("Pulverización terrestre", "PULV-TE"), ("Rolo", "ROL"), ("Rastra y rolo", "RAS-ROL"), ("Siembra fina", "SIEM-FIN"),
        ("Siembra gruesa", "SIEM-GRU"), ("Resiembra", "RE-SIEM"), ("Subsolador", "SUBSO")];
    var seededLabors = laborTypes.List();
    Check(seededLabors.Count == expectedLabors.Length && expectedLabors.All(expected =>
        seededLabors.Any(item => item.Name == expected.Name && item.Abbreviation == expected.Abbreviation && item.Revision == 1)), "seeded labors");
    var laborBefore = Snapshot();
    Reject<DomainValidationException>(() => laborTypes.Create(" ", null), "blank labor type");
    Reject<DomainValidationException>(() => laborTypes.Create("Labor larga", "ABCDEFGHIJKLM"), "abbreviation over 12 characters");
    Reject<DomainDuplicateException>(() => laborTypes.Create(" disco ", "D"), "duplicate labor");
    Check(Snapshot() == laborBefore, "rejected labor creates changed stored rows");
    var laborType = laborTypes.Create("Labor de prueba", "  LAB-PRU ");
    Check(laborType.Revision == 1 && laborType.Abbreviation == "LAB-PRU" && laborTypes.List().Any(item => item == laborType), "labor type creation/listing");
    Check(laborTypes.Create("Labor sin abreviatura", "   ").Abbreviation is null, "blank abbreviation stored as absent");
    laborBefore = Snapshot();
    Reject<DomainDuplicateException>(() => laborTypes.Update(laborType.Id, laborType.Revision, "DISCO", null), "rename to duplicate labor");
    Reject<DomainValidationException>(() => laborTypes.Update(laborType.Id, laborType.Revision, "Labor de prueba", "ABCDEFGHIJKLM"), "abbreviation update over 12 characters");
    Check(Snapshot() == laborBefore, "rejected labor updates changed stored rows");
    var changedLabor = laborTypes.Update(laborType.Id, laborType.Revision, "Labor de prueba", "ABCDEFGHIJKL");
    laborBefore = Snapshot();
    Reject<DomainConflictException>(() => laborTypes.Update(laborType.Id, laborType.Revision, "Stale", null), "stale labor");
    Reject<DomainNotFoundException>(() => laborTypes.Update(Guid.NewGuid(), 1, "Missing", null), "missing labor");
    Check(Snapshot() == laborBefore && changedLabor.Abbreviation == "ABCDEFGHIJKL" && changedLabor.Revision == 2, "labor update");

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

    void RejectSql(string sql, string label)
    {
        using var connection = new NpgsqlConnection(builder.ConnectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        try { command.ExecuteNonQuery(); }
        catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.CheckViolation) { return; }
        throw new Exception($"Expected check violation: {label}");
    }

    var stages = new StageCatalog(new PostgresStageRepository(builder.ConnectionString));
    var seededStages = stages.List();
    Check(seededStages.Select(item => (item.Name, item.SortOrder)).SequenceEqual([("Barbecho", 10), ("Presiembra", 20), ("Preemergencia", 30),
        ("Postsiembra", 40), ("Postemergencia", 50), ("Prefloración", 60), ("Postfloración", 70)]), "seeded stages in order");
    var stagesBefore = Snapshot();
    Reject<DomainValidationException>(() => stages.Create(" ", 80), "blank stage");
    Reject<DomainDuplicateException>(() => stages.Create(" BARBECHO ", 80), "duplicate stage");
    Reject<DomainDuplicateException>(() => stages.Update(seededStages[1].Id, seededStages[1].Revision, "barbecho", 20), "rename to duplicate stage");
    Check(Snapshot() == stagesBefore, "rejected stage writes changed stored rows");
    var stage = stages.Create(" Cosecha ", 5);
    Check(stage.Name == "Cosecha" && stage.Revision == 1 && stages.List()[0] == stage, "stage creation ordered by sort order");
    var movedStage = stages.Update(stage.Id, stage.Revision, "Cosecha", 80);
    stagesBefore = Snapshot();
    Reject<DomainConflictException>(() => stages.Update(stage.Id, stage.Revision, "Stale", 1), "stale stage");
    Reject<DomainNotFoundException>(() => stages.Update(Guid.NewGuid(), 1, "Missing", 1), "missing stage");
    Check(Snapshot() == stagesBefore && movedStage.Revision == 2 && stages.List()[^1] == movedStage, "stage update");

    var contractors = new ContractorCatalog(new PostgresContractorRepository(builder.ConnectionString));
    var ownContractor = contractors.List().Single();
    Check(ownContractor.Name == "Propio" && ownContractor.OwnEquipment && ownContractor.TaxId is null && ownContractor.Revision == 1, "seeded own contractor");
    var contractorDraft = new ContractorDraft(" Contratista de prueba ", " 20-12345678-9 ", " 0000 000000 ", " contacto@example.test ", "  ", false);
    var contractorsBefore = Snapshot();
    foreach (var taxId in new[] { "2012345678", "201234567890", "20-1234567-8x", "20.12345678.9", "--" })
        Reject<DomainValidationException>(() => contractors.Create(contractorDraft with { TaxId = taxId }), $"invalid CUIT {taxId}");
    foreach (var email in new[] { "sin-arroba", "a@b@c", "@example.test", "contacto@" })
        Reject<DomainValidationException>(() => contractors.Create(contractorDraft with { Email = email }), $"invalid email {email}");
    Reject<DomainValidationException>(() => contractors.Create(contractorDraft with { Name = " " }), "blank contractor");
    Reject<DomainDuplicateException>(() => contractors.Create(contractorDraft with { Name = "  PROPIO " }), "duplicate contractor");
    Check(Snapshot() == contractorsBefore, "rejected contractor creates changed stored rows");
    var contractor = contractors.Create(contractorDraft);
    Check(contractor.Name == "Contratista de prueba" && contractor.TaxId == "20123456789" && contractor.Phone == "0000 000000" &&
          contractor.Email == "contacto@example.test" && contractor.Notes is null && !contractor.OwnEquipment && contractor.Revision == 1 &&
          contractors.List().Contains(contractor), "contractor creation/listing");
    contractorsBefore = Snapshot();
    Reject<DomainDuplicateException>(() => contractors.Update(contractor.Id, contractor.Revision, contractorDraft with { Name = "propio" }), "rename to duplicate contractor");
    Reject<DomainValidationException>(() => contractors.Update(contractor.Id, contractor.Revision, contractorDraft with { TaxId = "123" }), "invalid CUIT update");
    Reject<DomainValidationException>(() => contractors.Update(contractor.Id, contractor.Revision, contractorDraft with { Email = "a@b@c" }), "invalid email update");
    Check(Snapshot() == contractorsBefore, "rejected contractor updates changed stored rows");
    var changedContractor = contractors.Update(contractor.Id, contractor.Revision,
        new ContractorDraft("Contratista de prueba", "20 12345678 9", null, null, "Notas", true));
    Check(changedContractor.TaxId == "20123456789" && changedContractor.Phone is null && changedContractor.Email is null &&
          changedContractor.Notes == "Notas" && changedContractor.OwnEquipment && changedContractor.Revision == 2, "contractor update");
    contractorsBefore = Snapshot();
    Reject<DomainConflictException>(() => contractors.Update(contractor.Id, contractor.Revision, contractorDraft), "stale contractor");
    Reject<DomainNotFoundException>(() => contractors.Update(Guid.NewGuid(), 1, contractorDraft), "missing contractor");
    Check(Snapshot() == contractorsBefore, "stale contractor write changed stored rows");

    var inputs = new InputCatalog(new PostgresInputRepository(builder.ConnectionString));
    Check(inputs.List().Count == 0, "inputs are not seeded");
    var inputDraft = new InputDraft(" Producto de prueba ", " Principio activo ficticio ", " 48% ", " SC ", 3, " Herbicida ", " l ", 1.5m, 3m, 10.25m, true);
    var inputsBefore = Snapshot();
    foreach (var invalid in new[] {
        inputDraft with { CommercialName = " " }, inputDraft with { Unit = " " }, inputDraft with { Category = "semilla" },
        inputDraft with { MixOrder = 0 }, inputDraft with { MixOrder = 21 }, inputDraft with { DoseMin = 0m }, inputDraft with { DoseMin = -1m },
        inputDraft with { DoseMax = 0m }, inputDraft with { DoseMin = 4m }, inputDraft with { DoseMin = 0.00001m },
        inputDraft with { ReferenceCost = -1m }, inputDraft with { ReferenceCost = 0.00001m } })
        Reject<DomainValidationException>(() => inputs.Create(invalid), $"invalid input {invalid}");
    Check(Snapshot() == inputsBefore, "rejected input creates changed stored rows");
    var input = inputs.Create(inputDraft);
    Check(input.CommercialName == "Producto de prueba" && input.ActiveIngredient == "Principio activo ficticio" && input.Concentration == "48%" &&
          input.Formulation == "SC" && input.MixOrder == 3 && input.Category == "herbicida" && input.Unit == "l" && input.DoseMin == 1.5m &&
          input.DoseMax == 3m && input.ReferenceCost == 10.25m && input.HollowCone && input.Revision == 1 && inputs.List().Contains(input), "input creation/listing");
    var minimalInput = inputs.Create(new InputDraft("Producto mínimo", null, " ", null, null, null, "kg", 2m, 2m, null, false));
    Check(minimalInput.ActiveIngredient is null && minimalInput.Concentration is null && minimalInput.Category is null && minimalInput.MixOrder is null &&
          minimalInput.DoseMin == 2m && minimalInput.DoseMax == 2m && minimalInput.ReferenceCost is null && !minimalInput.HollowCone, "minimal input");
    inputsBefore = Snapshot();
    Reject<DomainDuplicateException>(() => inputs.Create(inputDraft with { CommercialName = "  PRODUCTO DE PRUEBA " }), "duplicate input");
    Reject<DomainDuplicateException>(() => inputs.Update(minimalInput.Id, minimalInput.Revision, inputDraft), "rename to duplicate input");
    Reject<DomainValidationException>(() => inputs.Update(input.Id, input.Revision, inputDraft with { DoseMin = 5m }), "invalid input update");
    Check(Snapshot() == inputsBefore, "rejected input writes changed stored rows");
    var changedInput = inputs.Update(input.Id, input.Revision, inputDraft with { Category = "fungicida", DoseMax = null });
    Check(changedInput.Category == "fungicida" && changedInput.DoseMax is null && changedInput.Revision == 2, "input update");
    inputsBefore = Snapshot();
    Reject<DomainConflictException>(() => inputs.Update(input.Id, input.Revision, inputDraft), "stale input");
    Reject<DomainNotFoundException>(() => inputs.Update(Guid.NewGuid(), 1, inputDraft), "missing input");
    Check(Snapshot() == inputsBefore, "stale input write changed stored rows");

    // 007: client attributes. CUIT and email follow the contractor rules.
    var clientDraft = new ClientDraft(" Cliente con datos ", " 30-12345678-9 ", " Productor Uno, Productor Dos ", " cliente@example.test ",
        " 0000 000000 ", " Calle ficticia 1 ");
    var attributesBefore = Snapshot();
    foreach (var taxId in new[] { "2012345678", "201234567890", "20-1234567-8x", "20.12345678.9", "--" })
        Reject<DomainValidationException>(() => service.CreateClient(clientDraft with { TaxId = taxId }), $"invalid client CUIT {taxId}");
    foreach (var email in new[] { "sin-arroba", "a@b@c", "@example.test", "cliente@" })
        Reject<DomainValidationException>(() => service.CreateClient(clientDraft with { Email = email }), $"invalid client email {email}");
    Reject<DomainValidationException>(() => service.CreateClient(clientDraft with { Name = " " }), "blank client with attributes");
    Check(Snapshot() == attributesBefore, "rejected client creates changed stored rows");
    var fullClient = service.CreateClient(clientDraft);
    Check(fullClient is { Name: "Cliente con datos", TaxId: "30123456789", Producers: "Productor Uno, Productor Dos", Email: "cliente@example.test",
          Phone: "0000 000000", Address: "Calle ficticia 1", Revision: 1 } && service.GetClient(fullClient.Id) == fullClient &&
          service.ListClients().Contains(fullClient), "client attributes stored");
    attributesBefore = Snapshot();
    Reject<DomainValidationException>(() => service.UpdateClient(fullClient.Id, fullClient.Revision, clientDraft with { TaxId = "123" }), "invalid CUIT client update");
    Reject<DomainValidationException>(() => service.UpdateClient(fullClient.Id, fullClient.Revision, clientDraft with { Email = "a@b@c" }), "invalid email client update");
    Check(Snapshot() == attributesBefore, "rejected client updates changed stored rows");
    var clearedClient = service.UpdateClient(fullClient.Id, fullClient.Revision, new ClientDraft("Cliente con datos", Producers: "  "));
    Check(clearedClient is { TaxId: null, Producers: null, Email: null, Phone: null, Address: null, Revision: 2 }, "client update replaces every attribute");
    attributesBefore = Snapshot();
    Reject<DomainConflictException>(() => service.UpdateClient(fullClient.Id, fullClient.Revision, clientDraft), "stale client with attributes");
    Check(Snapshot() == attributesBefore, "stale client write changed stored rows");
    fullClient = service.UpdateClient(fullClient.Id, clearedClient.Revision, clientDraft);
    Check(fullClient is { TaxId: "30123456789", Revision: 3 }, "client attributes restored");

    // Field tenure and locality; the fields filter returns only one client's fields.
    attributesBefore = Snapshot();
    foreach (var tenure in new[] { "arrendado", "alquiler fijo", "propia" })
        Reject<DomainValidationException>(() => service.CreateField(fullClient.Id, new FieldDraft("Campo", tenure)), $"invalid tenure {tenure}");
    Reject<DomainNotFoundException>(() => service.CreateField(Guid.NewGuid(), new FieldDraft("Campo", "propio")), "field with attributes and missing client");
    Check(Snapshot() == attributesBefore, "rejected field creates changed stored rows");
    var rentedField = service.CreateField(fullClient.Id, new FieldDraft(" Campo alquilado ", " Alquiler_Fijo ", " Localidad ficticia "));
    Check(rentedField is { Name: "Campo alquilado", Tenure: "alquiler_fijo", Locality: "Localidad ficticia", Revision: 1 } &&
          service.GetField(rentedField.Id) == rentedField, "field attributes stored");
    attributesBefore = Snapshot();
    Reject<DomainValidationException>(() => service.UpdateField(rentedField.Id, rentedField.Revision, new FieldDraft("Campo alquilado", "arrendado")), "invalid tenure update");
    Check(Snapshot() == attributesBefore, "rejected field update changed stored rows");
    rentedField = service.UpdateField(rentedField.Id, rentedField.Revision, new FieldDraft("Campo alquilado", "alquiler_porcentaje"));
    Check(rentedField is { Tenure: "alquiler_porcentaje", Locality: null, Revision: 2 }, "field update replaces every attribute");
    attributesBefore = Snapshot();
    Reject<DomainConflictException>(() => service.UpdateField(rentedField.Id, 1, new FieldDraft("Stale", "propio")), "stale field with attributes");
    Check(Snapshot() == attributesBefore, "stale field write changed stored rows");
    var ownField = service.CreateField(fullClient.Id, new FieldDraft("Campo propio", "propio"));
    Check(service.ListFields(fullClient.Id).Select(item => item.Id).Order().SequenceEqual(new[] { rentedField.Id, ownField.Id }.Order()) &&
          service.ListFields(client.Id).Select(item => item.Id).SequenceEqual([field.Id]) && service.ListFields(Guid.NewGuid()).Count == 0 &&
          service.ListFields().Count == 3, "fields filtered by client");

    // Plot code: optional, trimmed and unique without regard to case; many plots may have none.
    var codedPlot = service.CreatePlot(ownField.Id, new PlotDraft(" Lote 1 ", 10m, " CLI - CAMPO - LOTE 1 "));
    Check(codedPlot is { Name: "Lote 1", Code: "CLI - CAMPO - LOTE 1", Revision: 1 } && service.GetPlot(codedPlot.Id) == codedPlot, "plot code stored");
    attributesBefore = Snapshot();
    Check(Rejected<DomainDuplicateException>(() => service.CreatePlot(ownField.Id, new PlotDraft("Lote 2", 5m, " cli - campo - lote 1 ")), "duplicate plot code")
          .Message == "Duplicate code", "duplicate plot code message");
    Reject<DomainValidationException>(() => service.CreatePlot(ownField.Id, new PlotDraft("Lote 2", 5m, new string('X', 41))), "plot code over 40 characters");
    Check(Snapshot() == attributesBefore, "rejected plot creates changed stored rows");
    var blankCodePlot = service.CreatePlot(ownField.Id, new PlotDraft("Lote 2", 5m, "  "));
    var noCodePlot = service.CreatePlot(ownField.Id, new PlotDraft("Lote 3", 5m));
    Check(blankCodePlot.Code is null && noCodePlot.Code is null, "plots without code");
    attributesBefore = Snapshot();
    Reject<DomainDuplicateException>(() => service.UpdatePlot(blankCodePlot.Id, blankCodePlot.Revision, new PlotDraft("Lote 2", 5m, "Cli - Campo - Lote 1")), "update to duplicate plot code");
    Check(Snapshot() == attributesBefore, "rejected plot code update changed stored rows");
    var uncodedPlot = service.UpdatePlot(codedPlot.Id, codedPlot.Revision, new PlotDraft("Lote 1", 10m));
    var recodedPlot = service.UpdatePlot(blankCodePlot.Id, blankCodePlot.Revision, new PlotDraft("Lote 2", 5m, "cli - campo - lote 1"));
    Check(uncodedPlot is { Code: null, Revision: 2 } && recodedPlot is { Code: "cli - campo - lote 1", Revision: 2 }, "plot code replaced and reused");
    Check(service.ListPlots(ownField.Id).Select(item => item.Id).Order().SequenceEqual(new[] { codedPlot.Id, blankCodePlot.Id, noCodePlot.Id }.Order()) &&
          service.ListPlots(field.Id).Select(item => item.Id).SequenceEqual([plot.Id]) && service.ListPlots(Guid.NewGuid()).Count == 0 &&
          service.ListPlots().Count == 4, "plots filtered by field");

    // Commercial firms belong to one client; names are unique across every client.
    var firms = new CommercialFirmCatalog(new PostgresCommercialFirmRepository(builder.ConnectionString));
    attributesBefore = Snapshot();
    Reject<DomainValidationException>(() => firms.Create(fullClient.Id, " ", null), "blank firm");
    Reject<DomainValidationException>(() => firms.Create(fullClient.Id, "Firma", "123"), "invalid firm CUIT");
    Reject<DomainNotFoundException>(() => firms.Create(Guid.NewGuid(), "Firma", null), "firm with missing client");
    Check(Snapshot() == attributesBefore, "rejected firm creates changed stored rows");
    var firm = firms.Create(fullClient.Id, " Firma comercial ", " 30-11111111-1 ");
    Check(firm is { Name: "Firma comercial", TaxId: "30111111111", Revision: 1 } && firm.ClientId == fullClient.Id && firms.List().Contains(firm), "firm creation/listing");
    var secondFirm = firms.Create(fullClient.Id, "Otra firma", null);
    var otherClientFirm = firms.Create(client.Id, "Firma de otro cliente", null);
    attributesBefore = Snapshot();
    Reject<DomainDuplicateException>(() => firms.Create(client.Id, " FIRMA COMERCIAL ", null), "duplicate firm across clients");
    Reject<DomainDuplicateException>(() => firms.Update(secondFirm.Id, secondFirm.Revision, "firma comercial", null), "rename to duplicate firm");
    Reject<DomainValidationException>(() => firms.Update(firm.Id, firm.Revision, "Firma comercial", "1"), "invalid firm CUIT update");
    Check(Snapshot() == attributesBefore, "rejected firm writes changed stored rows");
    var changedFirm = firms.Update(firm.Id, firm.Revision, "Firma comercial", null);
    Check(changedFirm is { TaxId: null, Revision: 2 } && changedFirm.ClientId == fullClient.Id, "firm update");
    attributesBefore = Snapshot();
    Reject<DomainConflictException>(() => firms.Update(firm.Id, firm.Revision, "Stale", null), "stale firm");
    Reject<DomainNotFoundException>(() => firms.Update(Guid.NewGuid(), 1, "Missing", null), "missing firm");
    Check(Snapshot() == attributesBefore, "stale firm write changed stored rows");
    Check(firms.List(fullClient.Id).Select(item => item.Id).Order().SequenceEqual(new[] { firm.Id, secondFirm.Id }.Order()) &&
          firms.List(client.Id).Single() == otherClientFirm && firms.List().Count == 3, "firms filtered by client");

    // Plot crops: up to two per plot and campaign, each renta or servicio, with an optional hybrid or variety.
    var plotCrops = new PlotCropService(new PostgresPlotCropRepository(builder.ConnectionString));
    var wheat = crops.List().Single(item => item.Name == "Trigo");
    var soy = crops.List().Single(item => item.Name == "Soja");
    attributesBefore = Snapshot();
    foreach (short position in new short[] { 0, 3, -1 })
        Reject<DomainValidationException>(() => plotCrops.Create(plot.Id, campaign.Id, soy.Id, position, "renta", null), $"invalid position {position}");
    foreach (var role in new[] { "cobertura", " ", "rentas" })
        Reject<DomainValidationException>(() => plotCrops.Create(plot.Id, campaign.Id, soy.Id, 1, role, null), $"invalid role {role}");
    Reject<DomainNotFoundException>(() => plotCrops.Create(Guid.NewGuid(), campaign.Id, soy.Id, 1, "renta", null), "plot crop with missing plot");
    Reject<DomainNotFoundException>(() => plotCrops.Create(plot.Id, Guid.NewGuid(), soy.Id, 1, "renta", null), "plot crop with missing campaign");
    Reject<DomainNotFoundException>(() => plotCrops.Create(plot.Id, campaign.Id, Guid.NewGuid(), 1, "renta", null), "plot crop with missing crop");
    Check(Snapshot() == attributesBefore, "rejected plot crop creates changed stored rows");
    var firstCrop = plotCrops.Create(plot.Id, campaign.Id, wheat.Id, 1, " Servicio ", "  ");
    var secondCrop = plotCrops.Create(plot.Id, campaign.Id, soy.Id, 2, "renta", " Híbrido ficticio 1 ");
    Check(firstCrop is { Position: 1, Role: "servicio", Variety: null, Revision: 1 } && firstCrop.CropId == wheat.Id &&
          secondCrop is { Position: 2, Role: "renta", Variety: "Híbrido ficticio 1" } && plotCrops.List().Contains(secondCrop), "plot crop creation/listing");
    attributesBefore = Snapshot();
    Check(Rejected<DomainDuplicateException>(() => plotCrops.Create(plot.Id, campaign.Id, soy.Id, 1, "renta", null), "duplicate plot crop position")
          .Message == "Duplicate position", "duplicate position message");
    Check(Snapshot() == attributesBefore, "rejected duplicate position changed stored rows");
    var otherPlotCrop = plotCrops.Create(codedPlot.Id, campaign.Id, soy.Id, 1, "renta", null);
    var changedCropRow = plotCrops.Update(firstCrop.Id, firstCrop.Revision, soy.Id, "renta", "Variedad ficticia");
    Check(changedCropRow is { Position: 1, Role: "renta", Variety: "Variedad ficticia", Revision: 2 } && changedCropRow.CropId == soy.Id &&
          changedCropRow.PlotId == plot.Id && changedCropRow.CampaignId == campaign.Id, "plot crop update");
    attributesBefore = Snapshot();
    Reject<DomainNotFoundException>(() => plotCrops.Update(firstCrop.Id, changedCropRow.Revision, Guid.NewGuid(), "renta", null), "plot crop update to missing crop");
    Reject<DomainValidationException>(() => plotCrops.Update(firstCrop.Id, changedCropRow.Revision, soy.Id, "cobertura", null), "plot crop update with invalid role");
    Reject<DomainConflictException>(() => plotCrops.Update(firstCrop.Id, firstCrop.Revision, wheat.Id, "servicio", null), "stale plot crop");
    Reject<DomainNotFoundException>(() => plotCrops.Update(Guid.NewGuid(), 1, wheat.Id, "servicio", null), "missing plot crop");
    Check(Snapshot() == attributesBefore, "rejected plot crop updates changed stored rows");
    Check(plotCrops.List(plot.Id).Select(item => item.Id).SequenceEqual([firstCrop.Id, secondCrop.Id]) &&
          plotCrops.List(codedPlot.Id).Single() == otherPlotCrop && plotCrops.List(Guid.NewGuid()).Count == 0 && plotCrops.List().Count == 3,
          "plot crops filtered by plot");

    // Database constraints mirror the domain rules for writers that bypass the services.
    var sqlBefore = Snapshot();
    RejectSql("UPDATE labor_types SET abbreviation = 'ABCDEFGHIJKLM'", "labor abbreviation length");
    RejectSql("UPDATE contractors SET tax_id = '20-12345678-9'", "contractor CUIT digits");
    RejectSql("UPDATE contractors SET email = 'a@b@c'", "contractor email");
    RejectSql("UPDATE inputs SET category = 'semilla'", "input category");
    RejectSql("UPDATE inputs SET mix_order = 21", "input mix order");
    RejectSql("UPDATE inputs SET dose_min = 9, dose_max = 1", "input dose range");
    RejectSql("UPDATE inputs SET dose_min = 0", "input positive dose");
    RejectSql("UPDATE clients SET tax_id = '30-12345678-9'", "client CUIT digits");
    RejectSql("UPDATE clients SET email = 'a@b@c'", "client email");
    RejectSql("UPDATE fields SET tenure = 'arrendado'", "field tenure");
    RejectSql("UPDATE plots SET code = ' padded '", "plot code trimmed");
    RejectSql("UPDATE plot_crops SET position = 3", "plot crop position");
    RejectSql("UPDATE plot_crops SET role = 'cobertura'", "plot crop role");
    Check(Snapshot() == sqlBefore, "rejected SQL writes changed stored rows");

    var provenance = new ImportProvenanceService(new PostgresImportRecordRepository(builder.ConnectionString));
    var contractorRecord = provenance.RecordOnce("contractor", contractor.Id, "fixture:contractors:1", "{}");
    var inputRecord = provenance.RecordOnce("input", input.Id, "fixture:inputs:1", "{}");
    var clientRecord = provenance.RecordOnce("client", client.Id, "fixture:clients:1", "{}");
    Check(!contractorRecord.Verified && inputRecord.EntityType == "input" && clientRecord.EntityType == "client", "contractor and input provenance");
    var provenanceBefore = Snapshot();
    Reject<DomainValidationException>(() => provenance.RecordOnce("stage", stage.Id, "fixture:stages:1", "{}"), "unsupported provenance entity");
    RejectSql($"INSERT INTO import_records(id,entity_type,entity_id,source_reference,raw_payload) VALUES ('{Guid.NewGuid()}','stage','{stage.Id}','fixture','{{}}')", "provenance entity check");
    Check(Snapshot() == provenanceBefore, "rejected provenance changed stored rows");

    // 008: work orders, numbered consecutively per commercial firm inside the creating transaction.
    var workOrders = new WorkOrderService(new PostgresWorkOrderRepository(builder.ConnectionString));
    string WorkOrderStateOf(string connectionString)
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand("""
            SELECT concat_ws('|',
              (SELECT coalesce(string_agg(to_jsonb(w)::text, ',' ORDER BY w.id), '') FROM work_orders w),
              (SELECT coalesce(string_agg(to_jsonb(p)::text, ',' ORDER BY p.work_order_id, p.plot_id), '') FROM work_order_plots p),
              (SELECT coalesce(string_agg(to_jsonb(l)::text, ',' ORDER BY l.id), '') FROM work_order_lines l),
              (SELECT coalesce(string_agg(f.id || ':' || f.next_work_order_number, ',' ORDER BY f.id), '') FROM commercial_firms f))
            """, connection);
        return (string)command.ExecuteScalar()!;
    }
    string WorkOrderState() => WorkOrderStateOf(builder.ConnectionString);
    int NextNumber(Guid firmId) => firms.List().Single(item => item.Id == firmId).NextWorkOrderNumber;
    var issuedOn = new DateOnly(2026, 11, 24);
    // codedPlot has 10 ha and blankCodePlot 5 ha, both in ownField of fullClient; firm and secondFirm belong to fullClient.
    var orderDraft = new WorkOrderDraft(issuedOn, laborType.Id, stage.Id, contractor.Id, ownField.Id, campaign.Id, soy.Id,
        " Híbrido ficticio 1 ", " Observación ficticia ", " Ingeniero ficticio ",
        [new WorkOrderPlotDraft(codedPlot.Id, 10m), new WorkOrderPlotDraft(blankCodePlot.Id, 6.5m)],
        [new WorkOrderLineDraft(minimalInput.Id, 2m), new WorkOrderLineDraft(input.Id, 1.3333m)]);
    Check(firms.List().All(item => item.NextWorkOrderNumber == 1), "firms start numbering at 1");

    var orderBefore = WorkOrderState();
    Check(Rejected<DomainValidationException>(() => workOrders.Create(firm.Id, orderDraft with { Plots = [new WorkOrderPlotDraft(plot.Id, 1m)] }), "plot of another field")
          .Message == "Every plot must belong to the work order's field", "plot of another field message");
    Check(Rejected<DomainValidationException>(() => workOrders.Create(otherClientFirm.Id, orderDraft), "field of another client")
          .Message == "Field must belong to the firm's client", "field of another client message");
    Check(Rejected<DomainValidationException>(() => workOrders.Create(firm.Id, orderDraft with { Plots = [] }), "zero plots")
          .Message == "At least one plot is required", "zero plots message");
    Reject<DomainValidationException>(() => workOrders.Create(firm.Id, orderDraft with { Plots = null }), "absent plots");
    Check(Rejected<DomainValidationException>(() => workOrders.Create(firm.Id, orderDraft with { IssuedOn = null }), "missing date")
          .Message == "Issued on is required", "missing date message");
    Check(Rejected<DomainValidationException>(() => workOrders.Create(firm.Id, orderDraft with
          { Plots = [new WorkOrderPlotDraft(codedPlot.Id, 1m), new WorkOrderPlotDraft(codedPlot.Id, 2m)] }), "duplicate plot")
          .Message == "Each plot can appear only once", "duplicate plot message");
    foreach (var worked in new[] { 0m, -1m, 0.00001m, 100000000m })
        Check(Rejected<DomainValidationException>(() => workOrders.Create(firm.Id, orderDraft with { Plots = [new WorkOrderPlotDraft(codedPlot.Id, worked)] }), $"worked area {worked}")
              .Message == "Worked area must be positive and fit numeric(12,4)", "worked area message");
    foreach (var dose in new[] { 0m, -1m, 0.00001m, 100000000m })
        Check(Rejected<DomainValidationException>(() => workOrders.Create(firm.Id, orderDraft with { Lines = [new WorkOrderLineDraft(input.Id, dose)] }), $"dose {dose}")
              .Message == "Dose must be positive and fit numeric(12,4)", "dose message");
    Check(Rejected<DomainValidationException>(() => workOrders.Create(firm.Id, orderDraft with
          { Plots = [new WorkOrderPlotDraft(codedPlot.Id, 99999999m)], Lines = [new WorkOrderLineDraft(input.Id, 99999999m)] }), "total overflow")
          .Message == "Line total must be positive and fit numeric(14,4)", "total overflow message");
    Reject<DomainNotFoundException>(() => workOrders.Create(firm.Id, orderDraft with { Lines = [new WorkOrderLineDraft(Guid.NewGuid(), 1m)] }), "missing input");
    Reject<DomainNotFoundException>(() => workOrders.Create(firm.Id, orderDraft with { Plots = [new WorkOrderPlotDraft(Guid.NewGuid(), 1m)] }), "missing plot");
    Reject<DomainNotFoundException>(() => workOrders.Create(Guid.NewGuid(), orderDraft), "missing firm");
    Reject<DomainNotFoundException>(() => workOrders.Create(firm.Id, orderDraft with { LaborTypeId = Guid.NewGuid() }), "missing labor");
    Reject<DomainNotFoundException>(() => workOrders.Create(firm.Id, orderDraft with { StageId = Guid.NewGuid() }), "missing stage");
    Reject<DomainNotFoundException>(() => workOrders.Create(firm.Id, orderDraft with { ContractorId = Guid.NewGuid() }), "missing contractor");
    Reject<DomainNotFoundException>(() => workOrders.Create(firm.Id, orderDraft with { FieldId = Guid.NewGuid() }), "missing field");
    Reject<DomainNotFoundException>(() => workOrders.Create(firm.Id, orderDraft with { CampaignId = Guid.NewGuid() }), "missing campaign");
    Reject<DomainNotFoundException>(() => workOrders.Create(firm.Id, orderDraft with { CropId = Guid.NewGuid() }), "missing crop");
    Check(WorkOrderState() == orderBefore && NextNumber(firm.Id) == 1, "rejected work order creates changed stored rows or the next number");

    var firstOrder = workOrders.Create(firm.Id, orderDraft);
    Check(firstOrder is { Number: 1, Status: "emitida", CancelReason: null, Revision: 1, Variety: "Híbrido ficticio 1",
          Observations: "Observación ficticia", ResponsibleEngineer: "Ingeniero ficticio" } && firstOrder.FirmId == firm.Id &&
          firstOrder.IssuedOn == issuedOn && firstOrder.FieldId == ownField.Id && firstOrder.StageId == stage.Id && firstOrder.CropId == soy.Id,
          "first work order header");
    // Worked areas sum 16.5 ha: 2 × 16.5 = 33 and 1.3333 × 16.5 = 21.99945, rounded half away from zero to 21.9995.
    Check(firstOrder.Plots.Count == 2 &&
          firstOrder.Plots.Single(item => item.PlotId == codedPlot.Id) is { AreaHectares: 10m, WorkedAreaHectares: 10m, WorkedAreaExceedsPlot: false } &&
          firstOrder.Plots.Single(item => item.PlotId == blankCodePlot.Id) is { AreaHectares: 5m, WorkedAreaHectares: 6.5m, WorkedAreaExceedsPlot: true },
          "plot area snapshot and worked area flag");
    Check(firstOrder.Lines.Count == 2 &&
          firstOrder.Lines[0] is { Position: 1, DosePerHectare: 2m, Unit: "kg", Total: 33m, DoseOutOfRange: false } && firstOrder.Lines[0].InputId == minimalInput.Id &&
          firstOrder.Lines[1] is { Position: 2, DosePerHectare: 1.3333m, Unit: "l", Total: 21.9995m, DoseOutOfRange: true } && firstOrder.Lines[1].InputId == input.Id,
          "line totals, unit snapshot and dose range flag; the dose is kept");
    Check(NextNumber(firm.Id) == 2 && workOrders.Get(firstOrder.Id) is { Number: 1, Revision: 1 } detail && detail.Lines.Count == 2 && detail.Plots.Count == 2,
          "work order detail");
    var secondOrder = workOrders.Create(firm.Id, orderDraft with { Lines = [] });
    var otherFirmOrder = workOrders.Create(secondFirm.Id, orderDraft);
    Check(secondOrder is { Number: 2, Lines.Count: 0 } && otherFirmOrder.Number == 1 && NextNumber(firm.Id) == 3 && NextNumber(secondFirm.Id) == 2,
          "numbers consecutive per firm and independent between firms; zero lines allowed");
    Check(workOrders.Get(Guid.NewGuid()) is null, "unknown work order");

    // The next number is editable to continue the workbook numbering, but never back onto an issued number.
    orderBefore = WorkOrderState();
    var secondFirmNow = firms.List().Single(item => item.Id == secondFirm.Id);
    foreach (var next in new[] { 1, 0, -5 })
        Check(Rejected<DomainValidationException>(() => firms.Update(secondFirm.Id, secondFirmNow.Revision, secondFirmNow.Name, null, next), $"next number {next}")
              .Message == "Next number must be greater than 1", "next number message");
    Check(Rejected<DomainValidationException>(() => firms.Update(otherClientFirm.Id, otherClientFirm.Revision, otherClientFirm.Name, null, 0), "next number 0 without orders")
          .Message == "Next number must be greater than 0", "next number message without orders");
    Reject<DomainConflictException>(() => firms.Update(secondFirm.Id, secondFirmNow.Revision + 1, secondFirmNow.Name, null, 50), "stale next number");
    Check(WorkOrderState() == orderBefore && firms.List().Single(item => item.Id == secondFirm.Id) == secondFirmNow, "rejected next numbers changed stored rows");
    var renumbered = firms.Update(secondFirm.Id, secondFirmNow.Revision, secondFirmNow.Name, null, 100);
    Check(renumbered is { NextWorkOrderNumber: 100 } && renumbered.Revision == secondFirmNow.Revision + 1, "custom next number stored");
    Check(workOrders.Create(secondFirm.Id, orderDraft).Number == 100 && NextNumber(secondFirm.Id) == 101, "custom next number honored");
    var renamed = firms.Update(secondFirm.Id, renumbered.Revision, "Otra firma SA", null);
    Check(renamed is { NextWorkOrderNumber: 101, Name: "Otra firma SA" }, "omitted next number unchanged");

    // Concurrent creates for one firm serialize on the firm row: distinct consecutive numbers, no gaps.
    using (var start = new Barrier(9))
    {
        var racers = Enumerable.Range(0, 8).Select(_ => Task.Run(() => { start.SignalAndWait(); return workOrders.Create(firm.Id, orderDraft).Number; })).ToArray();
        start.SignalAndWait();
        var numbers = (await Task.WhenAll(racers)).Order().ToArray();
        Check(numbers.SequenceEqual(Enumerable.Range(3, 8)) && NextNumber(firm.Id) == 11, $"concurrent numbers {string.Join(",", numbers)}");
    }
    orderBefore = WorkOrderState();
    Reject<DomainValidationException>(() => workOrders.Create(firm.Id, orderDraft with { Plots = [new WorkOrderPlotDraft(plot.Id, 1m)] }), "rejected create after race");
    Check(WorkOrderState() == orderBefore && NextNumber(firm.Id) == 11, "a rejected create leaves no gap");

    // Edit: full replace of header, plots and lines while emitida; firm and number never change.
    var editDraft = orderDraft with { StageId = null, CampaignId = null, CropId = null, Variety = " ", Observations = null, ResponsibleEngineer = null,
        Plots = [new WorkOrderPlotDraft(noCodePlot.Id, 5m)], Lines = [new WorkOrderLineDraft(input.Id, 2m)] };
    orderBefore = WorkOrderState();
    Reject<DomainValidationException>(() => workOrders.Update(firstOrder.Id, firstOrder.Revision, editDraft with { Plots = [new WorkOrderPlotDraft(plot.Id, 1m)] }), "edit to plot of another field");
    Reject<DomainValidationException>(() => workOrders.Update(firstOrder.Id, firstOrder.Revision, editDraft with { FieldId = field.Id, Plots = [new WorkOrderPlotDraft(plot.Id, 1m)] }), "edit to field of another client");
    Reject<DomainValidationException>(() => workOrders.Update(firstOrder.Id, firstOrder.Revision, editDraft with { Plots = [] }), "edit to zero plots");
    Reject<DomainNotFoundException>(() => workOrders.Update(firstOrder.Id, firstOrder.Revision, editDraft with { Lines = [new WorkOrderLineDraft(Guid.NewGuid(), 1m)] }), "edit to missing input");
    Reject<DomainNotFoundException>(() => workOrders.Update(Guid.NewGuid(), 1, editDraft), "edit of missing work order");
    Check(Rejected<DomainConflictException>(() => workOrders.Update(firstOrder.Id, firstOrder.Revision + 1, editDraft), "stale edit").Message == "Stale revision", "stale edit message");
    Check(WorkOrderState() == orderBefore, "rejected edits changed stored rows");
    var editedOrder = workOrders.Update(firstOrder.Id, firstOrder.Revision, editDraft);
    Check(editedOrder is { Number: 1, Revision: 2, Status: "emitida", StageId: null, CampaignId: null, CropId: null, Variety: null, Observations: null, ResponsibleEngineer: null } &&
          editedOrder.FirmId == firm.Id && editedOrder.Plots.Single() is { AreaHectares: 5m, WorkedAreaHectares: 5m, WorkedAreaExceedsPlot: false } &&
          editedOrder.Lines.Single() is { Position: 1, Total: 10m, DoseOutOfRange: false } && NextNumber(firm.Id) == 11, "edit replaces header, plots and lines");
    orderBefore = WorkOrderState();
    Reject<DomainConflictException>(() => workOrders.Update(firstOrder.Id, firstOrder.Revision, orderDraft), "edit with the replaced revision");
    Check(WorkOrderState() == orderBefore, "stale edit changed stored rows");

    // Cancel keeps the number, the order stays queryable and the next create takes the following number.
    Check(Rejected<DomainValidationException>(() => workOrders.Cancel(secondOrder.Id, secondOrder.Revision, "  "), "blank reason").Message == "Reason is required", "blank reason message");
    Reject<DomainConflictException>(() => workOrders.Cancel(secondOrder.Id, secondOrder.Revision + 1, "Motivo"), "stale cancel");
    Reject<DomainNotFoundException>(() => workOrders.Cancel(Guid.NewGuid(), 1, "Motivo"), "cancel of missing work order");
    Reject<DomainNotFoundException>(() => workOrders.Complete(Guid.NewGuid(), 1), "complete of missing work order");
    Reject<DomainConflictException>(() => workOrders.Complete(firstOrder.Id, firstOrder.Revision), "stale complete");
    Check(WorkOrderState() == orderBefore, "rejected cancel and complete changed stored rows");
    var cancelled = workOrders.Cancel(secondOrder.Id, secondOrder.Revision, " Lluvia ");
    Check(cancelled is { Number: 2, Status: "cancelada", CancelReason: "Lluvia", Revision: 2 } && workOrders.Get(secondOrder.Id) is { Status: "cancelada", Number: 2 },
          "cancelled order keeps its number and stays queryable");
    Check(workOrders.Create(firm.Id, orderDraft).Number == 11, "next create after cancel takes the following number");
    var completed = workOrders.Complete(editedOrder.Id, editedOrder.Revision);
    Check(completed is { Status: "realizada", CancelReason: null, Revision: 3, Number: 1 }, "complete");
    orderBefore = WorkOrderState();
    foreach (var (closed, label) in new[] { (cancelled, "cancelled"), (completed, "completed") })
    {
        Check(Rejected<DomainStateException>(() => workOrders.Update(closed.Id, closed.Revision, editDraft), $"edit {label}").Message == "Work order is not editable", $"edit {label} message");
        Check(Rejected<DomainStateException>(() => workOrders.Cancel(closed.Id, closed.Revision, "Motivo"), $"cancel {label}").Message == "Work order is not editable", $"cancel {label} message");
        Check(Rejected<DomainStateException>(() => workOrders.Complete(closed.Id, closed.Revision), $"complete {label}").Message == "Work order is not editable", $"complete {label} message");
    }
    Check(WorkOrderState() == orderBefore, "rejected transitions of closed orders changed stored rows");

    // Summary list, filtered by firm and status.
    var allOrders = workOrders.List();
    Check(allOrders.Count == 13 && workOrders.List(firm.Id).Count == 11 && workOrders.List(secondFirm.Id).Count == 2 && workOrders.List(Guid.NewGuid()).Count == 0,
          "work orders filtered by firm");
    Check(workOrders.List(status: "cancelada").Single() is { Number: 2, PlotCount: 2 } cancelledSummary && cancelledSummary.Id == secondOrder.Id &&
          workOrders.List(firm.Id, "realizada").Single().Id == firstOrder.Id && workOrders.List(secondFirm.Id, "emitida").Count == 2 &&
          allOrders.Single(item => item.Id == firstOrder.Id) is { PlotCount: 1, Revision: 3, Status: "realizada" }, "work orders filtered by status");
    Check(Rejected<DomainValidationException>(() => workOrders.List(status: "anulada"), "invalid status filter").Message ==
          "Status must be one of: emitida, realizada, cancelada", "invalid status filter message");

    orderBefore = WorkOrderState();
    RejectSql("UPDATE work_orders SET status = 'anulada'", "work order status");
    RejectSql("UPDATE work_orders SET status = 'cancelada', cancel_reason = NULL", "cancelled without reason");
    RejectSql("UPDATE work_orders SET cancel_reason = 'Motivo' WHERE status = 'emitida'", "reason without cancel");
    RejectSql("UPDATE work_order_plots SET worked_area_hectares = 0", "positive worked area");
    RejectSql("UPDATE work_order_lines SET dose_per_hectare = 0", "positive dose");
    RejectSql("UPDATE commercial_firms SET next_work_order_number = 0", "positive next number");
    Check(WorkOrderState() == orderBefore, "rejected work order SQL changed stored rows");

    // 006 on a database that already holds rows in every earlier table, including a same-name labor.
    Migrate(seedBuilder.ConnectionString, previousMigrations);
    var keptLaborId = Guid.NewGuid();
    var discoLaborId = Guid.NewGuid();
    using (var connection = new NpgsqlConnection(seedBuilder.ConnectionString))
    {
        connection.Open();
        using var existing = new NpgsqlCommand($$"""
            INSERT INTO clients(id,name) VALUES ('{{client.Id}}','Client');
            INSERT INTO fields(id,client_id,name) VALUES ('{{field.Id}}','{{client.Id}}','Field');
            INSERT INTO plots(id,field_id,name,area_hectares) VALUES ('{{plot.Id}}','{{field.Id}}','Plot',1);
            INSERT INTO crops(id,name) VALUES ('{{keptCropId}}','TRIGO');
            INSERT INTO campaigns(id,name) VALUES ('{{campaign.Id}}','2026-27');
            INSERT INTO plot_campaign_areas(id,plot_id,campaign_id,area_hectares) VALUES ('{{plotArea.Id}}','{{plot.Id}}','{{campaign.Id}}',1);
            INSERT INTO labor_types(id,name) VALUES ('{{keptLaborId}}','siembra GRUESA'), ('{{discoLaborId}}','DISCO');
            INSERT INTO recipes(id,plot_id,campaign_id,labor_type_id,stage,target_use,responsible_engineer)
              VALUES ('{{recipe.Id}}','{{plot.Id}}','{{campaign.Id}}','{{keptLaborId}}','presiembra','Barbecho','Responsable');
            INSERT INTO recipe_lines(id,recipe_id,product_name,dose_value,dose_unit) VALUES ('{{recipeLine.Id}}','{{recipe.Id}}','Producto',1,'l/ha');
            INSERT INTO import_records(id,entity_type,entity_id,source_reference,raw_payload) VALUES ('{{Guid.NewGuid()}}','client','{{client.Id}}','fixture','{}');
            """, connection);
        existing.ExecuteNonQuery();
    }
    Migrate(seedBuilder.ConnectionString, "006_catalogs.sql");
    var existingLabors = new LaborTypeCatalog(new PostgresLaborTypeRepository(seedBuilder.ConnectionString)).List();
    Check(existingLabors.Count == expectedLabors.Length &&
          existingLabors.Single(item => item.Id == keptLaborId) is { Name: "siembra GRUESA", Abbreviation: "SIEM-GRU" } &&
          existingLabors.Single(item => item.Id == discoLaborId) is { Name: "DISCO", Abbreviation: "DISCO" } &&
          expectedLabors.All(expected => existingLabors.Any(item => string.Equals(item.Name, expected.Name, StringComparison.OrdinalIgnoreCase) &&
              item.Abbreviation == expected.Abbreviation)), "seed kept same-name labors and filled abbreviations");
    using (var connection = new NpgsqlConnection(seedBuilder.ConnectionString))
    {
        connection.Open();
        using var rows = new NpgsqlCommand("""
            SELECT (SELECT count(*) FROM clients) + (SELECT count(*) FROM fields) + (SELECT count(*) FROM plots) + (SELECT count(*) FROM crops)
              + (SELECT count(*) FROM campaigns) + (SELECT count(*) FROM plot_campaign_areas) + (SELECT count(*) FROM recipes)
              + (SELECT count(*) FROM recipe_lines) + (SELECT count(*) FROM import_records)
            """, connection);
        Check((long)rows.ExecuteScalar()! == 9, "existing rows kept by 006");
        using var keep = new NpgsqlCommand($"UPDATE labor_types SET abbreviation = 'DSC' WHERE id = '{discoLaborId}'", connection);
        keep.ExecuteNonQuery();
    }
    var rerunBefore = SnapshotOf(seedBuilder.ConnectionString, withAttributes: false);
    Migrate(seedBuilder.ConnectionString, "006_catalogs.sql");
    Check(SnapshotOf(seedBuilder.ConnectionString, withAttributes: false) == rerunBefore, "rerunning 006 changed stored rows");

    // 007 on a database that already holds rows in every earlier table, including a same-name crop.
    using (var connection = new NpgsqlConnection(seedBuilder.ConnectionString))
    {
        connection.Open();
        using var existing = new NpgsqlCommand($"INSERT INTO inputs(id,commercial_name,unit) VALUES ('{Guid.NewGuid()}','Producto','l')", connection);
        existing.ExecuteNonQuery();
    }
    var earlierRows = SnapshotOf(seedBuilder.ConnectionString, withAttributes: false);
    Migrate(seedBuilder.ConnectionString, "007_attributes.sql");
    Check(SnapshotOf(seedBuilder.ConnectionString, withAttributes: false) == earlierRows, "007 changed rows stored by earlier migrations");
    var existingOperations = new OperationsService(new PostgresOperationsRepository(seedBuilder.ConnectionString));
    var existingCrops = new CropCatalog(new PostgresCropRepository(seedBuilder.ConnectionString)).List();
    Check(existingOperations.GetClient(client.Id) is { TaxId: null, Producers: null, Email: null, Phone: null, Address: null } &&
          existingOperations.GetField(field.Id) is { Tenure: null, Locality: null } && existingOperations.GetPlot(plot.Id) is { Code: null },
          "007 leaves new attributes of existing rows absent");
    Check(existingCrops.Count == referenceCrops.Length && existingCrops.Single(item => item.Id == keptCropId) is { Name: "TRIGO", Revision: 1 } &&
          referenceCrops.All(name => existingCrops.Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))),
          "crop seed kept the same-name crop and added the rest");
    rerunBefore = SnapshotOf(seedBuilder.ConnectionString) + "|" + AttributeTablesOf(seedBuilder.ConnectionString);
    Migrate(seedBuilder.ConnectionString, "007_attributes.sql");
    Check(SnapshotOf(seedBuilder.ConnectionString) + "|" + AttributeTablesOf(seedBuilder.ConnectionString) == rerunBefore, "rerunning 007 changed stored rows");

    // 008 on a database that already holds rows in every earlier table, then rerun with a stored work order.
    var seedFirmId = Guid.NewGuid();
    using (var connection = new NpgsqlConnection(seedBuilder.ConnectionString))
    {
        connection.Open();
        using var existing = new NpgsqlCommand($"""
            INSERT INTO commercial_firms(id,client_id,name) VALUES ('{seedFirmId}','{client.Id}','Firma');
            INSERT INTO plot_crops(id,plot_id,campaign_id,crop_id,position,role) VALUES ('{Guid.NewGuid()}','{plot.Id}','{campaign.Id}','{keptCropId}',1,'renta');
            """, connection);
        existing.ExecuteNonQuery();
    }
    earlierRows = SnapshotOf(seedBuilder.ConnectionString) + "|" + AttributeTablesOf(seedBuilder.ConnectionString);
    Migrate(seedBuilder.ConnectionString, "008_work_orders.sql");
    Check(SnapshotOf(seedBuilder.ConnectionString) + "|" + AttributeTablesOf(seedBuilder.ConnectionString, withNextNumber: false) == earlierRows,
          "008 changed rows stored by earlier migrations");
    var seedFirm = new CommercialFirmCatalog(new PostgresCommercialFirmRepository(seedBuilder.ConnectionString)).List().Single();
    Check(seedFirm is { NextWorkOrderNumber: 1, Revision: 1 }, "008 starts existing firms at number 1");
    var seedContractor = new ContractorCatalog(new PostgresContractorRepository(seedBuilder.ConnectionString)).List().Single();
    var seedOrder = new WorkOrderService(new PostgresWorkOrderRepository(seedBuilder.ConnectionString)).Create(seedFirmId,
        new WorkOrderDraft(issuedOn, keptLaborId, null, seedContractor.Id, field.Id, null, null, null, null, null, [new WorkOrderPlotDraft(plot.Id, 1m)], []));
    Check(seedOrder.Number == 1, "work order over migrated schema");
    rerunBefore = SnapshotOf(seedBuilder.ConnectionString) + "|" + AttributeTablesOf(seedBuilder.ConnectionString) + "|" + WorkOrderStateOf(seedBuilder.ConnectionString);
    Migrate(seedBuilder.ConnectionString, "008_work_orders.sql");
    Check(SnapshotOf(seedBuilder.ConnectionString) + "|" + AttributeTablesOf(seedBuilder.ConnectionString) + "|" + WorkOrderStateOf(seedBuilder.ConnectionString) == rerunBefore,
          "rerunning 008 changed stored rows");

    Console.WriteLine("PASS: isolated PostgreSQL service boundary, validation, hierarchy and its attributes, commercial firms, crops and their seed, plot crops, campaigns, labors, stages, contractors, inputs, plot campaign areas, recipes and work orders with concurrent revisions and numbering");
    return 0;
}
finally
{
    using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE; DROP SCHEMA \"{seedSchema}\" CASCADE", admin);
    drop.ExecuteNonQuery();
}
