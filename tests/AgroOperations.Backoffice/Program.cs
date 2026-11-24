using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using Npgsql;

var baseDatabase = Environment.GetEnvironmentVariable("AGRO_TEST_DATABASE");
if (string.IsNullOrWhiteSpace(baseDatabase)) { Console.Error.WriteLine("AGRO_TEST_DATABASE is required"); return 2; }
var parsedDatabase = new NpgsqlConnectionStringBuilder(baseDatabase);
if (parsedDatabase.Host is not ("127.0.0.1" or "localhost"))
    throw new Exception("Backoffice tests require a loopback-only PostgreSQL test server");
var schema = "test_" + Guid.NewGuid().ToString("N");
var scoped = new NpgsqlConnectionStringBuilder(baseDatabase) { SearchPath = schema };
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
using var admin = new NpgsqlConnection(baseDatabase);
admin.Open();
using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin)) create.ExecuteNonQuery();
try
{
    using (var db = new NpgsqlConnection(scoped.ConnectionString))
    {
        db.Open();
        foreach (var name in new[] { "001_operations.sql", "003_crops.sql", "004_agronomy.sql", "005_import_records.sql", "006_catalogs.sql" })
        {
            var file = Path.Combine(root, "src", "AgroOperations.Api", "Persistence", "Migrations", name);
            using var migration = new NpgsqlCommand(File.ReadAllText(file), db);
            migration.ExecuteNonQuery();
        }
    }
    using var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    var start = new ProcessStartInfo("dotnet")
    {
        ArgumentList = { "run", "--project", Path.Combine(root, "src", "AgroOperations.Api", "AgroOperations.Api.csproj"),
            "--no-launch-profile", "--", "--urls", $"http://127.0.0.1:{port}" },
        UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
    };
    start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    start.Environment["AGRO_LOCAL_ADMIN_EMAIL"] = "owner@example.test";
    start.Environment["AGRO_LOCAL_ADMIN_PASSWORD"] = password;
    start.Environment["AGRO_LOCAL_DATABASE"] = scoped.ConnectionString;
    using var server = Process.Start(start) ?? throw new Exception("Unable to start test API");
    try
    {
        using var handler = new HttpClientHandler { UseCookies = true };
        using var client = new HttpClient(handler) { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(10) };
        HttpResponseMessage? health = null;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            if (server.HasExited) throw new Exception($"Test API exited with {server.ExitCode}: {await server.StandardError.ReadToEndAsync()}");
            try { health = await client.GetAsync("/api/v1/health"); break; }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { await Task.Delay(250); }
        }
        if (health?.StatusCode != HttpStatusCode.OK) throw new Exception("Test API did not start");
        foreach (var route in new[] { "clients", "fields", "plots", "crops", "campaigns", "labor-types", "plot-campaign-areas", "recipes", "recipe-lines", "stages", "contractors", "inputs" })
            if ((await client.GetAsync($"/api/v1/{route}")).StatusCode != HttpStatusCode.Unauthorized ||
                (await client.PostAsJsonAsync($"/api/v1/{route}", new { name = "Example" })).StatusCode != HttpStatusCode.Unauthorized ||
                (await client.PutAsJsonAsync($"/api/v1/{route}/{Guid.NewGuid()}", new { name = "Example", revision = 1 })).StatusCode != HttpStatusCode.Unauthorized)
                throw new Exception($"Anonymous {route} route accessible");
        var login = await client.PostAsJsonAsync("/api/v1/session", new { password });
        if (login.StatusCode != HttpStatusCode.OK) throw new Exception("Local session unavailable");
        using var session = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var csrf = session.RootElement.GetProperty("csrfToken").GetString()!;
        if ((await client.PostAsJsonAsync("/api/v1/clients", new { name = "Example" })).StatusCode != HttpStatusCode.Forbidden)
            throw new Exception("Business write accepted without CSRF");
        async Task<int> Count(string route) => (await client.GetFromJsonAsync<JsonElement[]>($"/api/v1/{route}"))!.Length;
        var catalogCounts = new Dictionary<string, int> { ["labor-types"] = 14, ["stages"] = 7, ["contractors"] = 1, ["inputs"] = 0 };
        var catalogBody = new { name = "Example", abbreviation = "EX", sortOrder = 1, commercialName = "Example", unit = "l", revision = 1 };
        foreach (var (route, expected) in catalogCounts)
        {
            var anyId = (await client.GetFromJsonAsync<JsonElement[]>($"/api/v1/{route}")) is { Length: > 0 } rows ? rows[0].GetProperty("id").GetGuid() : Guid.NewGuid();
            if ((await client.PostAsJsonAsync($"/api/v1/{route}", catalogBody)).StatusCode != HttpStatusCode.Forbidden ||
                (await client.PutAsJsonAsync($"/api/v1/{route}/{anyId}", catalogBody)).StatusCode != HttpStatusCode.Forbidden ||
                await Count(route) != expected)
                throw new Exception($"{route} write accepted without CSRF or seed count differs");
        }
        async Task<HttpResponseMessage> Send(HttpMethod method, string path, object value)
        {
            using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(value) };
            request.Headers.Add("X-Agro-CSRF", csrf);
            return await client.SendAsync(request);
        }
        async Task<JsonElement> Body(HttpResponseMessage response) =>
            JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
        async Task<bool> IsError(HttpResponseMessage response, HttpStatusCode status, string? error = null) =>
            response.StatusCode == status && (error is null || (await Body(response)).GetProperty("error").GetString() == error);
        if ((await Send(HttpMethod.Post, "/api/v1/clients", new { name = " " })).StatusCode != HttpStatusCode.BadRequest ||
            (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/clients"))!.Length != 0)
            throw new Exception("Rejected client changed listing");
        var created = await Send(HttpMethod.Post, "/api/v1/clients", new { name = "Sample" });
        if (created.StatusCode != HttpStatusCode.Created) throw new Exception($"Client create: {(int)created.StatusCode}");
        using var clientRow = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = clientRow.RootElement.GetProperty("id").GetGuid();
        var list = await client.GetFromJsonAsync<JsonElement[]>("/api/v1/clients");
        if (list?.Length != 1 || list[0].GetProperty("id").GetGuid() != id) throw new Exception("Client list mismatch");
        if ((await Send(HttpMethod.Post, "/api/v1/fields", new { clientId = Guid.NewGuid(), name = "Orphan" })).StatusCode != HttpStatusCode.NotFound ||
            (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/fields"))!.Length != 0)
            throw new Exception("Orphan field changed stored records");
        var field = await Send(HttpMethod.Post, "/api/v1/fields", new { clientId = id, name = "Field" });
        if (field.StatusCode != HttpStatusCode.Created) throw new Exception("Field create failed");
        using var fieldRow = JsonDocument.Parse(await field.Content.ReadAsStringAsync());
        var fieldId = fieldRow.RootElement.GetProperty("id").GetGuid();
        if ((await Send(HttpMethod.Post, "/api/v1/plots", new { fieldId, name = "Invalid", areaHectares = 0m })).StatusCode != HttpStatusCode.BadRequest ||
            (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/plots"))!.Length != 0)
            throw new Exception("Invalid plot area changed stored records");
        var plot = await Send(HttpMethod.Post, "/api/v1/plots", new { fieldId, name = "Plot", areaHectares = 2.5m });
        if (plot.StatusCode != HttpStatusCode.Created) throw new Exception("Plot create failed");
        var crop = await Send(HttpMethod.Post, "/api/v1/crops", new { name = "Crop" });
        if (crop.StatusCode != HttpStatusCode.Created) throw new Exception("Crop create failed");
        if (!await IsError(await Send(HttpMethod.Post, "/api/v1/crops", new { name = " CROP " }), HttpStatusCode.Conflict, "Duplicate name") ||
            (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/crops"))!.Length != 1)
            throw new Exception("Duplicate crop changed stored records");
        if ((await client.GetFromJsonAsync<JsonElement[]>("/api/v1/fields"))!.Length != 1 ||
            (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/plots"))!.Length != 1 ||
            (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/crops"))!.Length != 1)
            throw new Exception("Master listings incomplete");
        if ((await Send(HttpMethod.Put, $"/api/v1/clients/{id}", new { name = "Stale", revision = 0 })).StatusCode != HttpStatusCode.Conflict ||
            (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/clients"))![0].GetProperty("name").GetString() != "Sample")
            throw new Exception("Stale client update overwrote record");

        var campaign = await Send(HttpMethod.Post, "/api/v1/campaigns", new { name = "2026-27" });
        if (campaign.StatusCode != HttpStatusCode.Created) throw new Exception("Campaign create failed");
        using var campaignRow = JsonDocument.Parse(await campaign.Content.ReadAsStringAsync());
        var campaignId = campaignRow.RootElement.GetProperty("id").GetGuid();
        if (!await IsError(await Send(HttpMethod.Post, "/api/v1/campaigns", new { name = "2026-27" }), HttpStatusCode.Conflict, "Duplicate name") ||
            (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/campaigns"))!.Length != 1)
            throw new Exception("Duplicate campaign changed stored records");
        var seededLabors = await client.GetFromJsonAsync<JsonElement[]>("/api/v1/labor-types");
        if (!seededLabors!.Any(item => item.GetProperty("name").GetString() == "Pulverización terrestre" && item.GetProperty("abbreviation").GetString() == "PULV-TE"))
            throw new Exception("Seeded labor abbreviation missing");
        var laborType = await Send(HttpMethod.Post, "/api/v1/labor-types", new { name = "Labor de prueba", abbreviation = "LAB-PRU" });
        if (laborType.StatusCode != HttpStatusCode.Created) throw new Exception("Labor type create failed");
        var laborTypeRow = await Body(laborType);
        var laborTypeId = laborTypeRow.GetProperty("id").GetGuid();
        if (laborTypeRow.GetProperty("abbreviation").GetString() != "LAB-PRU") throw new Exception("Labor abbreviation not returned");
        if (!await IsError(await Send(HttpMethod.Post, "/api/v1/labor-types", new { name = " disco ", abbreviation = "D" }), HttpStatusCode.Conflict, "Duplicate name") ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/labor-types", new { name = "Labor larga", abbreviation = "ABCDEFGHIJKLM" }), HttpStatusCode.BadRequest) ||
            await Count("labor-types") != 15)
            throw new Exception("Rejected labor changed stored records");
        var changedLabor = await Send(HttpMethod.Put, $"/api/v1/labor-types/{laborTypeId}", new { name = "Labor de prueba", abbreviation = "LP", revision = 1 });
        if (changedLabor.StatusCode != HttpStatusCode.OK || (await Body(changedLabor)).GetProperty("revision").GetInt64() != 2 ||
            (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/labor-types"))!.Single(item => item.GetProperty("id").GetGuid() == laborTypeId)
                .GetProperty("abbreviation").GetString() != "LP")
            throw new Exception("Labor abbreviation did not round-trip");
        if (!await IsError(await Send(HttpMethod.Put, $"/api/v1/labor-types/{laborTypeId}", new { name = "Stale", abbreviation = "S", revision = 1 }), HttpStatusCode.Conflict, "Stale revision"))
            throw new Exception("Stale labor update did not conflict");

        var stages = await client.GetFromJsonAsync<JsonElement[]>("/api/v1/stages");
        if (!stages!.Select(item => item.GetProperty("name").GetString()).SequenceEqual(
                ["Barbecho", "Presiembra", "Preemergencia", "Postsiembra", "Postemergencia", "Prefloración", "Postfloración"]) ||
            stages[6].GetProperty("sortOrder").GetInt32() != 70)
            throw new Exception("Seeded stages missing or out of order");
        var stage = await Send(HttpMethod.Post, "/api/v1/stages", new { name = "Cosecha", sortOrder = 80 });
        if (stage.StatusCode != HttpStatusCode.Created) throw new Exception($"Stage create: {(int)stage.StatusCode}");
        var stageId = (await Body(stage)).GetProperty("id").GetGuid();
        if (!await IsError(await Send(HttpMethod.Post, "/api/v1/stages", new { name = " barbecho ", sortOrder = 5 }), HttpStatusCode.Conflict, "Duplicate name") ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/stages", new { name = " ", sortOrder = 5 }), HttpStatusCode.BadRequest) ||
            await Count("stages") != 8)
            throw new Exception("Rejected stage changed stored records");
        var changedStage = await Send(HttpMethod.Put, $"/api/v1/stages/{stageId}", new { name = "Cosecha", sortOrder = 85, revision = 1 });
        if (changedStage.StatusCode != HttpStatusCode.OK || (await Body(changedStage)).GetProperty("sortOrder").GetInt32() != 85)
            throw new Exception("Stage update failed");
        if (!await IsError(await Send(HttpMethod.Put, $"/api/v1/stages/{stageId}", new { name = "Stale", sortOrder = 1, revision = 1 }), HttpStatusCode.Conflict, "Stale revision") ||
            (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/stages"))![^1].GetProperty("name").GetString() != "Cosecha")
            throw new Exception("Stale stage update overwrote record");

        var contractors = await client.GetFromJsonAsync<JsonElement[]>("/api/v1/contractors");
        if (contractors is not { Length: 1 } || contractors[0].GetProperty("name").GetString() != "Propio" || !contractors[0].GetProperty("ownEquipment").GetBoolean())
            throw new Exception("Seeded own contractor missing");
        if (!await IsError(await Send(HttpMethod.Post, "/api/v1/contractors", new { name = "Contratista", taxId = "20-1234567-8x" }), HttpStatusCode.BadRequest) ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/contractors", new { name = "Contratista", email = "a@b@c" }), HttpStatusCode.BadRequest) ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/contractors", new { name = " PROPIO " }), HttpStatusCode.Conflict, "Duplicate name") ||
            await Count("contractors") != 1)
            throw new Exception("Rejected contractor changed stored records");
        var contractor = await Send(HttpMethod.Post, "/api/v1/contractors",
            new { name = "Contratista de prueba", taxId = "20-12345678-9", phone = "0000 000000", email = "contacto@example.test", ownEquipment = false });
        if (contractor.StatusCode != HttpStatusCode.Created) throw new Exception($"Contractor create: {(int)contractor.StatusCode}");
        var contractorRow = await Body(contractor);
        var contractorId = contractorRow.GetProperty("id").GetGuid();
        if (contractorRow.GetProperty("taxId").GetString() != "20123456789" || await Count("contractors") != 2)
            throw new Exception("Contractor CUIT not normalized");
        var changedContractor = await Send(HttpMethod.Put, $"/api/v1/contractors/{contractorId}",
            new { name = "Contratista de prueba", taxId = "20123456789", notes = "Notas", ownEquipment = true, revision = 1 });
        if (changedContractor.StatusCode != HttpStatusCode.OK || (await Body(changedContractor)) is var contractorChange &&
            (contractorChange.GetProperty("notes").GetString() != "Notas" || !contractorChange.GetProperty("ownEquipment").GetBoolean()))
            throw new Exception("Contractor update failed");
        if (!await IsError(await Send(HttpMethod.Put, $"/api/v1/contractors/{contractorId}", new { name = "Stale", revision = 1 }), HttpStatusCode.Conflict, "Stale revision") ||
            !await IsError(await Send(HttpMethod.Put, $"/api/v1/contractors/{contractorId}", new { name = "Propio", revision = 2 }), HttpStatusCode.Conflict, "Duplicate name") ||
            (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/contractors"))!.Single(item => item.GetProperty("id").GetGuid() == contractorId)
                .GetProperty("name").GetString() != "Contratista de prueba")
            throw new Exception("Rejected contractor update overwrote record");

        var inputBody = new { commercialName = "Producto de prueba", activeIngredient = "Principio activo ficticio", concentration = "48%", formulation = "SC",
            mixOrder = 3, category = "herbicida", unit = "l", doseMin = 1.5m, doseMax = 3m, referenceCost = 10.25m, hollowCone = true };
        if (!await IsError(await Send(HttpMethod.Post, "/api/v1/inputs", inputBody with { category = "semilla" }), HttpStatusCode.BadRequest) ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/inputs", inputBody with { doseMin = 4m }), HttpStatusCode.BadRequest) ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/inputs", inputBody with { unit = " " }), HttpStatusCode.BadRequest) ||
            await Count("inputs") != 0)
            throw new Exception("Rejected input changed stored records");
        var input = await Send(HttpMethod.Post, "/api/v1/inputs", inputBody);
        if (input.StatusCode != HttpStatusCode.Created) throw new Exception($"Input create: {(int)input.StatusCode}");
        var inputRow = await Body(input);
        var inputId = inputRow.GetProperty("id").GetGuid();
        if (inputRow.GetProperty("doseMax").GetDecimal() != 3m || inputRow.GetProperty("mixOrder").GetInt32() != 3 || !inputRow.GetProperty("hollowCone").GetBoolean())
            throw new Exception("Input fields not returned");
        if (!await IsError(await Send(HttpMethod.Post, "/api/v1/inputs", inputBody with { commercialName = " PRODUCTO DE PRUEBA " }), HttpStatusCode.Conflict, "Duplicate name") ||
            await Count("inputs") != 1)
            throw new Exception("Duplicate input changed stored records");
        var changedInput = await Send(HttpMethod.Put, $"/api/v1/inputs/{inputId}", new { commercialName = "Producto de prueba", unit = "kg", category = "fungicida", revision = 1 });
        if (changedInput.StatusCode != HttpStatusCode.OK || (await Body(changedInput)).GetProperty("unit").GetString() != "kg")
            throw new Exception("Input update failed");
        if (!await IsError(await Send(HttpMethod.Put, $"/api/v1/inputs/{inputId}", new { commercialName = "Stale", unit = "l", revision = 1 }), HttpStatusCode.Conflict, "Stale revision") ||
            !await IsError(await Send(HttpMethod.Put, $"/api/v1/inputs/{inputId}", new { commercialName = "Producto de prueba", unit = "l", doseMin = 4m, doseMax = 3m, revision = 2 }), HttpStatusCode.BadRequest) ||
            (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/inputs"))![0].GetProperty("unit").GetString() != "kg")
            throw new Exception("Rejected input update overwrote record");
        if ((await Send(HttpMethod.Post, "/api/v1/plot-campaign-areas", new { plotId = Guid.NewGuid(), campaignId, areaHectares = 1m })).StatusCode != HttpStatusCode.NotFound ||
            (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/plot-campaign-areas"))!.Length != 0)
            throw new Exception("Orphan plot campaign area changed stored records");
        using var plotRow = JsonDocument.Parse(await (await client.GetAsync("/api/v1/plots")).Content.ReadAsStringAsync());
        var plotId = plotRow.RootElement[0].GetProperty("id").GetGuid();
        var createdArea = await Send(HttpMethod.Post, "/api/v1/plot-campaign-areas", new { plotId, campaignId, areaHectares = 2.75m });
        if (createdArea.StatusCode != HttpStatusCode.Created) throw new Exception("Plot campaign area create failed");
        if (!await IsError(await Send(HttpMethod.Post, "/api/v1/plot-campaign-areas", new { plotId, campaignId, areaHectares = 3m }), HttpStatusCode.BadRequest) ||
            (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/plot-campaign-areas"))!.Length != 1)
            throw new Exception("Duplicate plot campaign area changed stored records");
        var recipe = await Send(HttpMethod.Post, "/api/v1/recipes",
            new { plotId, campaignId, laborTypeId, stage = "presiembra", targetUse = "Barbecho", responsibleEngineer = "Matías Kincaid" });
        if (recipe.StatusCode != HttpStatusCode.Created) throw new Exception("Recipe create failed");
        using var recipeRow = JsonDocument.Parse(await recipe.Content.ReadAsStringAsync());
        var recipeId = recipeRow.RootElement.GetProperty("id").GetGuid();
        if ((await Send(HttpMethod.Post, "/api/v1/recipes", new { plotId, campaignId, laborTypeId, stage = "invalida", targetUse = "Barbecho", responsibleEngineer = "Matías Kincaid" })).StatusCode != HttpStatusCode.BadRequest ||
            (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/recipes"))!.Length != 1)
            throw new Exception("Invalid-stage recipe changed stored records");
        var recipeLine = await Send(HttpMethod.Post, "/api/v1/recipe-lines", new { recipeId, productName = "Glifosato equivalente ácido", doseValue = 2m, doseUnit = "l/ha" });
        if (recipeLine.StatusCode != HttpStatusCode.Created) throw new Exception("Recipe line create failed");
        if ((await Send(HttpMethod.Post, "/api/v1/recipe-lines", new { recipeId, productName = " ", doseValue = 2m, doseUnit = "l/ha" })).StatusCode != HttpStatusCode.BadRequest ||
            (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/recipe-lines"))!.Length != 1)
            throw new Exception("Invalid recipe line changed stored records");
        if ((await Send(HttpMethod.Put, $"/api/v1/recipes/{recipeId}", new { stage = "postemergencia", targetUse = "Barbecho", responsibleEngineer = "Matías Kincaid", revision = 0 })).StatusCode != HttpStatusCode.Conflict)
            throw new Exception("Stale recipe update did not conflict");
        using (var anonymous = new HttpClient { BaseAddress = client.BaseAddress })
            if ((await anonymous.GetAsync("/api/v1/import-records")).StatusCode != HttpStatusCode.Unauthorized)
                throw new Exception("Anonymous import-records route accessible");
        var importRecords = await client.GetFromJsonAsync<JsonElement[]>("/api/v1/import-records");
        if (importRecords?.Length != 0) throw new Exception("Unexpected import-records in fresh schema");
        // A wrong persisted DB password must fail startup, not serve a healthy but unusable API.
        using var spare = new TcpListener(IPAddress.Loopback, 0);
        spare.Start();
        var refusedPort = ((IPEndPoint)spare.LocalEndpoint).Port;
        spare.Stop();
        var wrongDatabase = new NpgsqlConnectionStringBuilder(scoped.ConnectionString) { Password = "invalid-test-password" };
        var refusedStart = new ProcessStartInfo("dotnet")
        {
            ArgumentList = { "run", "--no-build", "--project", Path.Combine(root, "src", "AgroOperations.Api", "AgroOperations.Api.csproj"),
                "--no-launch-profile", "--", "--urls", $"http://127.0.0.1:{refusedPort}" },
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
        };
        refusedStart.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        refusedStart.Environment["AGRO_LOCAL_ADMIN_EMAIL"] = "owner@example.test";
        refusedStart.Environment["AGRO_LOCAL_ADMIN_PASSWORD"] = password;
        refusedStart.Environment["AGRO_LOCAL_DATABASE"] = wrongDatabase.ConnectionString;
        using var refused = Process.Start(refusedStart) ?? throw new Exception("Unable to start invalid-DB API probe");
        try
        {
            if (!refused.WaitForExit(10000)) throw new Exception("Invalid DB password left the API running");
            if (refused.ExitCode == 0) throw new Exception("Invalid DB password did not reject startup");
        }
        finally { if (!refused.HasExited) { refused.Kill(entireProcessTree: true); await refused.WaitForExitAsync(); } }
        if ((await client.GetFromJsonAsync<JsonElement[]>("/api/v1/clients")) is not { Length: 1 })
            throw new Exception("Rejected DB startup changed client listing");
        Console.WriteLine("PASS: private HTTP CRUD, rejection invariants and revision guards in isolated PostgreSQL");
        return 0;
    }
    finally { if (!server.HasExited) server.Kill(entireProcessTree: true); await server.WaitForExitAsync(); }
}
finally
{
    using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin);
    drop.ExecuteNonQuery();
}
