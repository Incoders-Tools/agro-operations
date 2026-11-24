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
    Migrate(builder.ConnectionString, [.. previousMigrations, "006_catalogs.sql"]);
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

    string Snapshot() => SnapshotOf(builder.ConnectionString);
    string SnapshotOf(string connectionString)
    {
        using var connection = new NpgsqlConnection(connectionString);
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
              (SELECT coalesce(string_agg(to_jsonb(rl)::text, ',' ORDER BY rl.id), '') FROM recipe_lines rl),
              (SELECT coalesce(string_agg(to_jsonb(s)::text, ',' ORDER BY s.id), '') FROM stages s),
              (SELECT coalesce(string_agg(to_jsonb(c)::text, ',' ORDER BY c.id), '') FROM contractors c),
              (SELECT coalesce(string_agg(to_jsonb(i)::text, ',' ORDER BY i.id), '') FROM inputs i),
              (SELECT coalesce(string_agg(to_jsonb(ir)::text, ',' ORDER BY ir.id), '') FROM import_records ir))
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
    Reject<DomainDuplicateException>(() => crops.Create("  TRIGO  "), "duplicate crop");
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

    // Database constraints mirror the domain rules for writers that bypass the services.
    RejectSql("UPDATE labor_types SET abbreviation = 'ABCDEFGHIJKLM'", "labor abbreviation length");
    RejectSql("UPDATE contractors SET tax_id = '20-12345678-9'", "contractor CUIT digits");
    RejectSql("UPDATE contractors SET email = 'a@b@c'", "contractor email");
    RejectSql("UPDATE inputs SET category = 'semilla'", "input category");
    RejectSql("UPDATE inputs SET mix_order = 21", "input mix order");
    RejectSql("UPDATE inputs SET dose_min = 9, dose_max = 1", "input dose range");
    RejectSql("UPDATE inputs SET dose_min = 0", "input positive dose");
    Check(Snapshot() == inputsBefore, "rejected SQL writes changed stored rows");

    var provenance = new ImportProvenanceService(new PostgresImportRecordRepository(builder.ConnectionString));
    var contractorRecord = provenance.RecordOnce("contractor", contractor.Id, "fixture:contractors:1", "{}");
    var inputRecord = provenance.RecordOnce("input", input.Id, "fixture:inputs:1", "{}");
    var clientRecord = provenance.RecordOnce("client", client.Id, "fixture:clients:1", "{}");
    Check(!contractorRecord.Verified && inputRecord.EntityType == "input" && clientRecord.EntityType == "client", "contractor and input provenance");
    var provenanceBefore = Snapshot();
    Reject<DomainValidationException>(() => provenance.RecordOnce("stage", stage.Id, "fixture:stages:1", "{}"), "unsupported provenance entity");
    RejectSql($"INSERT INTO import_records(id,entity_type,entity_id,source_reference,raw_payload) VALUES ('{Guid.NewGuid()}','stage','{stage.Id}','fixture','{{}}')", "provenance entity check");
    Check(Snapshot() == provenanceBefore, "rejected provenance changed stored rows");

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
            INSERT INTO crops(id,name) VALUES ('{{crop.Id}}','Trigo');
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
    var rerunBefore = SnapshotOf(seedBuilder.ConnectionString);
    Migrate(seedBuilder.ConnectionString, "006_catalogs.sql");
    Check(SnapshotOf(seedBuilder.ConnectionString) == rerunBefore, "rerunning 006 changed stored rows");

    Console.WriteLine("PASS: isolated PostgreSQL service boundary, validation, hierarchy, crops, campaigns, labors, stages, contractors, inputs, plot campaign areas and recipes with concurrent revisions");
    return 0;
}
finally
{
    using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE; DROP SCHEMA \"{seedSchema}\" CASCADE", admin);
    drop.ExecuteNonQuery();
}
