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
        foreach (var name in new[] { "001_operations.sql", "003_crops.sql", "004_agronomy.sql", "005_import_records.sql", "006_catalogs.sql", "007_attributes.sql" })
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
        foreach (var route in new[] { "clients", "fields", "plots", "crops", "campaigns", "labor-types", "plot-campaign-areas", "recipes", "recipe-lines", "stages", "contractors", "inputs", "commercial-firms", "plot-crops" })
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
        foreach (var route in new[] { "fields?clientId=" + Guid.NewGuid(), "plots?fieldId=" + Guid.NewGuid(), "commercial-firms?clientId=" + Guid.NewGuid(), "plot-crops?plotId=" + Guid.NewGuid() })
            using (var anonymous = new HttpClient { BaseAddress = client.BaseAddress })
                if ((await anonymous.GetAsync($"/api/v1/{route}")).StatusCode != HttpStatusCode.Unauthorized)
                    throw new Exception($"Anonymous filtered {route} accessible");
        var catalogCounts = new Dictionary<string, int> { ["labor-types"] = 14, ["stages"] = 7, ["contractors"] = 1, ["inputs"] = 0,
            ["crops"] = 28, ["commercial-firms"] = 0, ["plot-crops"] = 0 };
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
        if (!(await client.GetFromJsonAsync<JsonElement[]>("/api/v1/crops"))!.Any(item => item.GetProperty("name").GetString() == "Pastura de gramíneas"))
            throw new Exception("Seeded reference crop missing");
        var crop = await Send(HttpMethod.Post, "/api/v1/crops", new { name = "Crop" });
        if (crop.StatusCode != HttpStatusCode.Created) throw new Exception("Crop create failed");
        if (!await IsError(await Send(HttpMethod.Post, "/api/v1/crops", new { name = " CROP " }), HttpStatusCode.Conflict, "Duplicate name") ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/crops", new { name = " soja " }), HttpStatusCode.Conflict, "Duplicate name") ||
            await Count("crops") != 29)
            throw new Exception("Duplicate crop changed stored records");
        if ((await client.GetFromJsonAsync<JsonElement[]>("/api/v1/fields"))!.Length != 1 ||
            (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/plots"))!.Length != 1 ||
            await Count("crops") != 29)
            throw new Exception("Master listings incomplete");
        if ((await Send(HttpMethod.Put, $"/api/v1/clients/{id}", new { name = "Stale", revision = 0 })).StatusCode != HttpStatusCode.Conflict ||
            (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/clients"))![0].GetProperty("name").GetString() != "Sample")
            throw new Exception("Stale client update overwrote record");

        // 007 attributes: old payloads keep working and every response carries the new properties.
        static bool Absent(JsonElement row, params string[] names) => names.All(name => row.GetProperty(name).ValueKind == JsonValueKind.Null);
        var sampleClient = await client.GetFromJsonAsync<JsonElement>($"/api/v1/clients/{id}");
        var sampleField = await client.GetFromJsonAsync<JsonElement>($"/api/v1/fields/{fieldId}");
        var samplePlot = (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/plots"))![0];
        if (!Absent(sampleClient, "taxId", "producers", "email", "phone", "address") || !Absent(sampleField, "tenure", "locality") || !Absent(samplePlot, "code"))
            throw new Exception("Old-shape records do not expose absent attributes");
        var clientBody = new { name = "Cliente con datos", taxId = "30-12345678-9", producers = "Productor Uno", email = "cliente@example.test",
            phone = "0000 000000", address = "Calle ficticia 1" };
        if (!await IsError(await Send(HttpMethod.Post, "/api/v1/clients", clientBody with { taxId = "123" }), HttpStatusCode.BadRequest) ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/clients", clientBody with { email = "a@b@c" }), HttpStatusCode.BadRequest) ||
            await Count("clients") != 1)
            throw new Exception("Rejected client attributes changed stored records");
        var richClient = await Send(HttpMethod.Post, "/api/v1/clients", clientBody);
        var richClientRow = await Body(richClient);
        var richId = richClientRow.GetProperty("id").GetGuid();
        if (richClient.StatusCode != HttpStatusCode.Created || richClientRow.GetProperty("taxId").GetString() != "30123456789" ||
            richClientRow.GetProperty("producers").GetString() != "Productor Uno" || richClientRow.GetProperty("address").GetString() != "Calle ficticia 1")
            throw new Exception($"Client with attributes: {(int)richClient.StatusCode}");
        if (!await IsError(await Send(HttpMethod.Put, $"/api/v1/clients/{richId}", new { name = "Cliente con datos", email = "a@b@c", revision = 1 }), HttpStatusCode.BadRequest) ||
            (await client.GetFromJsonAsync<JsonElement>($"/api/v1/clients/{richId}")).GetProperty("email").GetString() != "cliente@example.test")
            throw new Exception("Rejected client attribute update changed stored record");
        var replacedClient = await Send(HttpMethod.Put, $"/api/v1/clients/{richId}", new { name = "Cliente con datos", revision = 1 });
        if (replacedClient.StatusCode != HttpStatusCode.OK || !Absent(await Body(replacedClient), "taxId", "producers", "email", "phone", "address"))
            throw new Exception("Old-shape client update did not replace attributes");
        if (!await IsError(await Send(HttpMethod.Put, $"/api/v1/clients/{richId}", new { name = "Stale", taxId = "30-12345678-9", revision = 1 }), HttpStatusCode.Conflict, "Stale revision") ||
            !Absent(await client.GetFromJsonAsync<JsonElement>($"/api/v1/clients/{richId}"), "taxId"))
            throw new Exception("Stale client attribute update overwrote record");

        if (!await IsError(await Send(HttpMethod.Post, "/api/v1/fields", new { clientId = richId, name = "Campo", tenure = "arrendado" }), HttpStatusCode.BadRequest) ||
            await Count("fields") != 1)
            throw new Exception("Rejected field tenure changed stored records");
        var richField = await Send(HttpMethod.Post, "/api/v1/fields", new { clientId = richId, name = "Campo alquilado", tenure = "alquiler_fijo", locality = "Localidad ficticia" });
        var richFieldRow = await Body(richField);
        var richFieldId = richFieldRow.GetProperty("id").GetGuid();
        if (richField.StatusCode != HttpStatusCode.Created || richFieldRow.GetProperty("tenure").GetString() != "alquiler_fijo" ||
            richFieldRow.GetProperty("locality").GetString() != "Localidad ficticia")
            throw new Exception("Field with attributes failed");
        var changedField = await Send(HttpMethod.Put, $"/api/v1/fields/{richFieldId}", new { name = "Campo alquilado", tenure = "alquiler_porcentaje", revision = 1 });
        if (changedField.StatusCode != HttpStatusCode.OK || (await Body(changedField)) is var changedFieldRow &&
            (changedFieldRow.GetProperty("tenure").GetString() != "alquiler_porcentaje" || !Absent(changedFieldRow, "locality")))
            throw new Exception("Field attribute update failed");
        var oldShapeField = await Send(HttpMethod.Put, $"/api/v1/fields/{fieldId}", new { name = "Field", revision = 1 });
        if (oldShapeField.StatusCode != HttpStatusCode.OK || (await Body(oldShapeField)).GetProperty("revision").GetInt64() != 2)
            throw new Exception("Old-shape field update failed");
        if (!await IsError(await Send(HttpMethod.Put, $"/api/v1/fields/{richFieldId}", new { name = "Stale", revision = 1 }), HttpStatusCode.Conflict, "Stale revision") ||
            !await IsError(await Send(HttpMethod.Put, $"/api/v1/fields/{richFieldId}", new { name = "Campo", tenure = "arrendado", revision = 2 }), HttpStatusCode.BadRequest) ||
            (await client.GetFromJsonAsync<JsonElement>($"/api/v1/fields/{richFieldId}")).GetProperty("tenure").GetString() != "alquiler_porcentaje")
            throw new Exception("Rejected field update overwrote record");
        async Task<Guid[]> Ids(string route) => (await client.GetFromJsonAsync<JsonElement[]>($"/api/v1/{route}"))!.Select(item => item.GetProperty("id").GetGuid()).ToArray();
        if (!(await Ids($"fields?clientId={richId}")).SequenceEqual([richFieldId]) || !(await Ids($"fields?clientId={id}")).SequenceEqual([fieldId]) ||
            (await Ids($"fields?clientId={Guid.NewGuid()}")).Length != 0 || (await Ids("fields")).Length != 2)
            throw new Exception("Fields filter by client mismatch");

        var codedPlot = await Send(HttpMethod.Post, "/api/v1/plots", new { fieldId = richFieldId, name = "Lote", areaHectares = 3m, code = "CLI - CAMPO - LOTE" });
        var codedPlotRow = await Body(codedPlot);
        var codedPlotId = codedPlotRow.GetProperty("id").GetGuid();
        if (codedPlot.StatusCode != HttpStatusCode.Created || codedPlotRow.GetProperty("code").GetString() != "CLI - CAMPO - LOTE")
            throw new Exception("Plot with code failed");
        if (!await IsError(await Send(HttpMethod.Post, "/api/v1/plots", new { fieldId = richFieldId, name = "Otro", areaHectares = 1m, code = " cli - campo - lote " }), HttpStatusCode.Conflict, "Duplicate code") ||
            !await IsError(await Send(HttpMethod.Put, $"/api/v1/plots/{samplePlot.GetProperty("id").GetGuid()}", new { name = "Plot", areaHectares = 2.5m, code = "Cli - Campo - Lote", revision = 1 }), HttpStatusCode.Conflict, "Duplicate code") ||
            await Count("plots") != 2 || !Absent((await client.GetFromJsonAsync<JsonElement[]>($"/api/v1/plots?fieldId={fieldId}"))![0], "code"))
            throw new Exception("Duplicate plot code changed stored records");
        var oldShapePlot = await Send(HttpMethod.Put, $"/api/v1/plots/{codedPlotId}", new { name = "Lote", areaHectares = 3m, revision = 1 });
        if (oldShapePlot.StatusCode != HttpStatusCode.OK || !Absent(await Body(oldShapePlot), "code"))
            throw new Exception("Old-shape plot update did not replace code");
        if (!(await Ids($"plots?fieldId={richFieldId}")).SequenceEqual([codedPlotId]) ||
            !(await Ids($"plots?fieldId={fieldId}")).SequenceEqual([samplePlot.GetProperty("id").GetGuid()]) ||
            (await Ids($"plots?fieldId={Guid.NewGuid()}")).Length != 0 || (await Ids("plots")).Length != 2)
            throw new Exception("Plots filter by field mismatch");

        if (!await IsError(await Send(HttpMethod.Post, "/api/v1/commercial-firms", new { clientId = Guid.NewGuid(), name = "Firma" }), HttpStatusCode.NotFound, "Record not found") ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/commercial-firms", new { clientId = richId, name = " " }), HttpStatusCode.BadRequest) ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/commercial-firms", new { clientId = richId, name = "Firma", taxId = "123" }), HttpStatusCode.BadRequest) ||
            await Count("commercial-firms") != 0)
            throw new Exception("Rejected firm changed stored records");
        var firm = await Send(HttpMethod.Post, "/api/v1/commercial-firms", new { clientId = richId, name = "Firma comercial", taxId = "30-11111111-1" });
        var firmRow = await Body(firm);
        var firmId = firmRow.GetProperty("id").GetGuid();
        if (firm.StatusCode != HttpStatusCode.Created || firmRow.GetProperty("taxId").GetString() != "30111111111" || firmRow.GetProperty("clientId").GetGuid() != richId)
            throw new Exception($"Firm create: {(int)firm.StatusCode}");
        if (!await IsError(await Send(HttpMethod.Post, "/api/v1/commercial-firms", new { clientId = id, name = " FIRMA COMERCIAL " }), HttpStatusCode.Conflict, "Duplicate name") ||
            await Count("commercial-firms") != 1)
            throw new Exception("Duplicate firm changed stored records");
        // The update body has no client: a firm never moves to another client, even if one is sent.
        var changedFirm = await Send(HttpMethod.Put, $"/api/v1/commercial-firms/{firmId}", new { clientId = id, name = "Firma comercial SA", revision = 1 });
        if (changedFirm.StatusCode != HttpStatusCode.OK || (await Body(changedFirm)) is var changedFirmRow &&
            (changedFirmRow.GetProperty("clientId").GetGuid() != richId || !Absent(changedFirmRow, "taxId") || changedFirmRow.GetProperty("revision").GetInt64() != 2))
            throw new Exception("Firm update failed or moved the firm");
        if (!await IsError(await Send(HttpMethod.Put, $"/api/v1/commercial-firms/{firmId}", new { name = "Stale", revision = 1 }), HttpStatusCode.Conflict, "Stale revision") ||
            !await IsError(await Send(HttpMethod.Put, $"/api/v1/commercial-firms/{firmId}", new { name = "Firma", taxId = "1", revision = 2 }), HttpStatusCode.BadRequest) ||
            !await IsError(await Send(HttpMethod.Put, $"/api/v1/commercial-firms/{Guid.NewGuid()}", new { name = "Missing", revision = 1 }), HttpStatusCode.NotFound) ||
            (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/commercial-firms"))![0].GetProperty("name").GetString() != "Firma comercial SA")
            throw new Exception("Rejected firm update overwrote record");
        if (!(await Ids($"commercial-firms?clientId={richId}")).SequenceEqual([firmId]) || (await Ids($"commercial-firms?clientId={id}")).Length != 0)
            throw new Exception("Firms filter by client mismatch");

        var campaign = await Send(HttpMethod.Post, "/api/v1/campaigns", new { name = "2026-27" });
        if (campaign.StatusCode != HttpStatusCode.Created) throw new Exception("Campaign create failed");
        using var campaignRow = JsonDocument.Parse(await campaign.Content.ReadAsStringAsync());
        var campaignId = campaignRow.RootElement.GetProperty("id").GetGuid();
        if (!await IsError(await Send(HttpMethod.Post, "/api/v1/campaigns", new { name = "2026-27" }), HttpStatusCode.Conflict, "Duplicate name") ||
            (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/campaigns"))!.Length != 1)
            throw new Exception("Duplicate campaign changed stored records");
        var seededCrops = await client.GetFromJsonAsync<JsonElement[]>("/api/v1/crops");
        var soyId = seededCrops!.Single(item => item.GetProperty("name").GetString() == "Soja").GetProperty("id").GetGuid();
        var wheatId = seededCrops!.Single(item => item.GetProperty("name").GetString() == "Trigo").GetProperty("id").GetGuid();
        var plotCropBody = new { plotId = codedPlotId, campaignId, cropId = soyId, position = 1, role = "renta", variety = "NK 855 VIP3" };
        if (!await IsError(await Send(HttpMethod.Post, "/api/v1/plot-crops", plotCropBody with { position = 3 }), HttpStatusCode.BadRequest) ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/plot-crops", plotCropBody with { role = "cobertura" }), HttpStatusCode.BadRequest) ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/plot-crops", plotCropBody with { plotId = Guid.NewGuid() }), HttpStatusCode.NotFound, "Record not found") ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/plot-crops", plotCropBody with { campaignId = Guid.NewGuid() }), HttpStatusCode.NotFound, "Record not found") ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/plot-crops", plotCropBody with { cropId = Guid.NewGuid() }), HttpStatusCode.NotFound, "Record not found") ||
            await Count("plot-crops") != 0)
            throw new Exception("Rejected plot crop changed stored records");
        var plotCrop = await Send(HttpMethod.Post, "/api/v1/plot-crops", plotCropBody);
        var plotCropRow = await Body(plotCrop);
        var plotCropId = plotCropRow.GetProperty("id").GetGuid();
        if (plotCrop.StatusCode != HttpStatusCode.Created || plotCropRow.GetProperty("variety").GetString() != "NK 855 VIP3" ||
            plotCropRow.GetProperty("position").GetInt32() != 1 || plotCropRow.GetProperty("role").GetString() != "renta")
            throw new Exception($"Plot crop create: {(int)plotCrop.StatusCode}");
        if (!await IsError(await Send(HttpMethod.Post, "/api/v1/plot-crops", plotCropBody with { cropId = wheatId }), HttpStatusCode.Conflict, "Duplicate position") ||
            await Count("plot-crops") != 1)
            throw new Exception("Duplicate plot crop position changed stored records");
        var serviceCrop = await Send(HttpMethod.Post, "/api/v1/plot-crops", plotCropBody with { cropId = wheatId, position = 2, role = "servicio", variety = (string?)null });
        if (!await IsError(await Send(HttpMethod.Post, "/api/v1/plot-crops", plotCropBody with { position = 2 }), HttpStatusCode.Conflict, "Duplicate position"))
            throw new Exception("Third plot crop accepted");
        if (serviceCrop.StatusCode != HttpStatusCode.Created || !Absent(await Body(serviceCrop), "variety")) throw new Exception("Second plot crop failed");
        var changedPlotCrop = await Send(HttpMethod.Put, $"/api/v1/plot-crops/{plotCropId}", new { cropId = wheatId, role = "servicio", revision = 1 });
        if (changedPlotCrop.StatusCode != HttpStatusCode.OK || (await Body(changedPlotCrop)) is var changedPlotCropRow &&
            (changedPlotCropRow.GetProperty("cropId").GetGuid() != wheatId || changedPlotCropRow.GetProperty("position").GetInt32() != 1 ||
             !Absent(changedPlotCropRow, "variety") || changedPlotCropRow.GetProperty("plotId").GetGuid() != codedPlotId))
            throw new Exception("Plot crop update failed");
        if (!await IsError(await Send(HttpMethod.Put, $"/api/v1/plot-crops/{plotCropId}", new { cropId = soyId, role = "renta", revision = 1 }), HttpStatusCode.Conflict, "Stale revision") ||
            !await IsError(await Send(HttpMethod.Put, $"/api/v1/plot-crops/{plotCropId}", new { cropId = Guid.NewGuid(), role = "renta", revision = 2 }), HttpStatusCode.NotFound) ||
            !await IsError(await Send(HttpMethod.Put, $"/api/v1/plot-crops/{plotCropId}", new { cropId = soyId, role = "cobertura", revision = 2 }), HttpStatusCode.BadRequest) ||
            (await client.GetFromJsonAsync<JsonElement[]>($"/api/v1/plot-crops?plotId={codedPlotId}"))![0].GetProperty("cropId").GetGuid() != wheatId)
            throw new Exception("Rejected plot crop update overwrote record");
        if ((await Ids($"plot-crops?plotId={codedPlotId}")).Length != 2 || (await Ids($"plot-crops?plotId={samplePlot.GetProperty("id").GetGuid()}")).Length != 0 ||
            (await Ids("plot-crops")).Length != 2)
            throw new Exception("Plot crops filter by plot mismatch");
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
        if ((await client.GetFromJsonAsync<JsonElement[]>("/api/v1/clients")) is not { Length: 2 })
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
