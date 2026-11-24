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
        foreach (var name in new[] { "001_operations.sql", "003_crops.sql", "004_agronomy.sql", "005_import_records.sql", "006_catalogs.sql", "007_attributes.sql", "008_work_orders.sql", "009_ot_across_fields.sql", "010_reference_alignment.sql", "011_providers_and_quotes.sql" })
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
    // Drain the API's output: an unread pipe fills, then the console logger's queue, and requests block on logging.
    var serverLog = new System.Collections.Concurrent.ConcurrentQueue<string>();
    void Keep(string? line) { if (line is null) return; serverLog.Enqueue(line); while (serverLog.Count > 200) serverLog.TryDequeue(out _); }
    server.OutputDataReceived += (_, e) => Keep(e.Data);
    server.ErrorDataReceived += (_, e) => Keep(e.Data);
    server.BeginOutputReadLine();
    server.BeginErrorReadLine();
    try
    {
        using var handler = new HttpClientHandler { UseCookies = true };
        using var client = new HttpClient(handler) { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(10) };
        HttpResponseMessage? health = null;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            if (server.HasExited) throw new Exception($"Test API exited with {server.ExitCode}: {string.Join(Environment.NewLine, serverLog)}");
            try { health = await client.GetAsync("/api/v1/health"); break; }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { await Task.Delay(250); }
        }
        if (health?.StatusCode != HttpStatusCode.OK) throw new Exception("Test API did not start");
        foreach (var route in new[] { "clients", "fields", "plots", "crops", "campaigns", "labor-types", "plot-campaign-areas", "recipes", "recipe-lines", "stages", "contractors", "inputs", "commercial-firms", "plot-crops", "work-orders" })
            if ((await client.GetAsync($"/api/v1/{route}")).StatusCode != HttpStatusCode.Unauthorized ||
                (await client.PostAsJsonAsync($"/api/v1/{route}", new { name = "Example" })).StatusCode != HttpStatusCode.Unauthorized ||
                (await client.PutAsJsonAsync($"/api/v1/{route}/{Guid.NewGuid()}", new { name = "Example", revision = 1 })).StatusCode != HttpStatusCode.Unauthorized)
                throw new Exception($"Anonymous {route} route accessible");
        var anonymousOrder = Guid.NewGuid();
        if ((await client.GetAsync($"/api/v1/work-orders/{anonymousOrder}")).StatusCode != HttpStatusCode.Unauthorized ||
            (await client.GetAsync($"/api/v1/work-orders/{anonymousOrder}/document")).StatusCode != HttpStatusCode.Unauthorized ||
            (await client.GetAsync("/api/v1/work-orders?q=1")).StatusCode != HttpStatusCode.Unauthorized ||
            (await client.GetAsync($"/api/v1/work-orders?firmId={anonymousOrder}&status=emitida")).StatusCode != HttpStatusCode.Unauthorized ||
            (await client.PostAsJsonAsync($"/api/v1/work-orders/{anonymousOrder}/cancel", new { revision = 1, reason = "Motivo" })).StatusCode != HttpStatusCode.Unauthorized ||
            (await client.PostAsJsonAsync($"/api/v1/work-orders/{anonymousOrder}/complete", new { revision = 1 })).StatusCode != HttpStatusCode.Unauthorized)
            throw new Exception("Anonymous work order route accessible");
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
        var catalogCounts = new Dictionary<string, int> { ["labor-types"] = 82, ["stages"] = 7, ["contractors"] = 1, ["inputs"] = 0,
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
        // 009: creating a client also creates its commercial firm with the client's name.
        if ((await client.GetFromJsonAsync<JsonElement[]>($"/api/v1/commercial-firms?clientId={id}")) is not [var defaultFirm] ||
            defaultFirm.GetProperty("name").GetString() != "Sample" || defaultFirm.GetProperty("nextWorkOrderNumber").GetInt32() != 1)
            throw new Exception("Client create did not create its firm");
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
        if (!(await client.GetFromJsonAsync<JsonElement[]>("/api/v1/crops"))!.Any(item => item.GetProperty("name").GetString() == "Pasturas puras de gramíneas"))
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
            await Count("commercial-firms") != 2)
            throw new Exception("Rejected firm changed stored records");
        var firm = await Send(HttpMethod.Post, "/api/v1/commercial-firms", new { clientId = richId, name = "Firma comercial", taxId = "30-11111111-1" });
        var firmRow = await Body(firm);
        var firmId = firmRow.GetProperty("id").GetGuid();
        if (firm.StatusCode != HttpStatusCode.Created || firmRow.GetProperty("taxId").GetString() != "30111111111" || firmRow.GetProperty("clientId").GetGuid() != richId)
            throw new Exception($"Firm create: {(int)firm.StatusCode}");
        if (!await IsError(await Send(HttpMethod.Post, "/api/v1/commercial-firms", new { clientId = id, name = " FIRMA COMERCIAL " }), HttpStatusCode.Conflict, "Duplicate name") ||
            await Count("commercial-firms") != 3)
            throw new Exception("Duplicate firm changed stored records");
        // The update body has no client: a firm never moves to another client, even if one is sent.
        var changedFirm = await Send(HttpMethod.Put, $"/api/v1/commercial-firms/{firmId}", new { clientId = id, name = "Firma comercial SA", revision = 1 });
        if (changedFirm.StatusCode != HttpStatusCode.OK || (await Body(changedFirm)) is var changedFirmRow &&
            (changedFirmRow.GetProperty("clientId").GetGuid() != richId || !Absent(changedFirmRow, "taxId") || changedFirmRow.GetProperty("revision").GetInt64() != 2))
            throw new Exception("Firm update failed or moved the firm");
        if (!await IsError(await Send(HttpMethod.Put, $"/api/v1/commercial-firms/{firmId}", new { name = "Stale", revision = 1 }), HttpStatusCode.Conflict, "Stale revision") ||
            !await IsError(await Send(HttpMethod.Put, $"/api/v1/commercial-firms/{firmId}", new { name = "Firma", taxId = "1", revision = 2 }), HttpStatusCode.BadRequest) ||
            !await IsError(await Send(HttpMethod.Put, $"/api/v1/commercial-firms/{Guid.NewGuid()}", new { name = "Missing", revision = 1 }), HttpStatusCode.NotFound) ||
            (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/commercial-firms"))!.Single(item => item.GetProperty("id").GetGuid() == firmId)
                .GetProperty("name").GetString() != "Firma comercial SA")
            throw new Exception("Rejected firm update overwrote record");
        var richFirms = await Ids($"commercial-firms?clientId={richId}");
        if (richFirms.Length != 2 || richFirms[1] != firmId || (await Ids($"commercial-firms?clientId={id}")).Length != 1)
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
        var plotCropBody = new { plotId = codedPlotId, campaignId, cropId = soyId, position = 1, role = "renta", variety = "Híbrido ficticio 1" };
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
        if (plotCrop.StatusCode != HttpStatusCode.Created || plotCropRow.GetProperty("variety").GetString() != "Híbrido ficticio 1" ||
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
             !Absent(changedPlotCropRow, "variety", "grainYieldKgHa", "forageBiomassKgHa") || changedPlotCropRow.GetProperty("plotId").GetGuid() != codedPlotId))
            throw new Exception("Plot crop update failed");
        if (!await IsError(await Send(HttpMethod.Put, $"/api/v1/plot-crops/{plotCropId}", new { cropId = soyId, role = "renta", revision = 1 }), HttpStatusCode.Conflict, "Stale revision") ||
            !await IsError(await Send(HttpMethod.Put, $"/api/v1/plot-crops/{plotCropId}", new { cropId = Guid.NewGuid(), role = "renta", revision = 2 }), HttpStatusCode.NotFound) ||
            !await IsError(await Send(HttpMethod.Put, $"/api/v1/plot-crops/{plotCropId}", new { cropId = soyId, role = "cobertura", revision = 2 }), HttpStatusCode.BadRequest) ||
            (await client.GetFromJsonAsync<JsonElement[]>($"/api/v1/plot-crops?plotId={codedPlotId}"))![0].GetProperty("cropId").GetGuid() != wheatId)
            throw new Exception("Rejected plot crop update overwrote record");
        if ((await Ids($"plot-crops?plotId={codedPlotId}")).Length != 2 || (await Ids($"plot-crops?plotId={samplePlot.GetProperty("id").GetGuid()}")).Length != 0 ||
            (await Ids("plot-crops")).Length != 2)
            throw new Exception("Plot crops filter by plot mismatch");

        // 010: grain yield and forage biomass round-trip; the PUT replaces both.
        var samplePlotCropBody = new { plotId = samplePlot.GetProperty("id").GetGuid(), campaignId, cropId = soyId, position = 1, role = "renta",
            grainYieldKgHa = (decimal?)3500.5m, forageBiomassKgHa = (decimal?)12000m };
        var plotCropsBefore = await client.GetStringAsync("/api/v1/plot-crops");
        if (!await IsError(await Send(HttpMethod.Post, "/api/v1/plot-crops", samplePlotCropBody with { grainYieldKgHa = -1m }), HttpStatusCode.BadRequest,
                "Yield must be zero or positive and fit numeric(12,2)") ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/plot-crops", samplePlotCropBody with { forageBiomassKgHa = 0.001m }), HttpStatusCode.BadRequest,
                "Yield must be zero or positive and fit numeric(12,2)") ||
            !await IsError(await Send(HttpMethod.Put, $"/api/v1/plot-crops/{plotCropId}", new { cropId = wheatId, role = "servicio", grainYieldKgHa = -1m, revision = 2 }),
                HttpStatusCode.BadRequest, "Yield must be zero or positive and fit numeric(12,2)") ||
            await client.GetStringAsync("/api/v1/plot-crops") != plotCropsBefore)
            throw new Exception("Rejected yields changed stored records");
        var yieldCrop = await Send(HttpMethod.Post, "/api/v1/plot-crops", samplePlotCropBody);
        var yieldCropRow = await Body(yieldCrop);
        var yieldCropId = yieldCropRow.GetProperty("id").GetGuid();
        if (yieldCrop.StatusCode != HttpStatusCode.Created || yieldCropRow.GetProperty("grainYieldKgHa").GetDecimal() != 3500.5m ||
            yieldCropRow.GetProperty("forageBiomassKgHa").GetDecimal() != 12000m ||
            (await client.GetFromJsonAsync<JsonElement[]>($"/api/v1/plot-crops?plotId={samplePlotCropBody.plotId}"))!.Single().GetProperty("grainYieldKgHa").GetDecimal() != 3500.5m)
            throw new Exception($"Plot crop with yields: {(int)yieldCrop.StatusCode}");
        var replacedYields = await Send(HttpMethod.Put, $"/api/v1/plot-crops/{yieldCropId}", new { cropId = soyId, role = "renta", forageBiomassKgHa = 0m, revision = 1 });
        if (replacedYields.StatusCode != HttpStatusCode.OK || (await Body(replacedYields)) is var replacedYieldsRow &&
            (!Absent(replacedYieldsRow, "grainYieldKgHa") || replacedYieldsRow.GetProperty("forageBiomassKgHa").GetDecimal() != 0m ||
             replacedYieldsRow.GetProperty("revision").GetInt64() != 2))
            throw new Exception("Plot crop yields PUT did not replace both yields");
        var seededLabors = await client.GetFromJsonAsync<JsonElement[]>("/api/v1/labor-types");
        if (!seededLabors!.Any(item => item.GetProperty("name").GetString() == "Pulverización terrestre" && item.GetProperty("abbreviation").GetString() == "PULV-TE" &&
                item.GetProperty("category").GetString() == "aplicacion" && item.GetProperty("description").ValueKind == JsonValueKind.Null) ||
            !seededLabors!.Any(item => item.GetProperty("name").GetString() == "Rastra de disco pesada" && item.GetProperty("category").GetString() == "labranza" &&
                item.GetProperty("description").GetString() == "excéntrica o \"rastra tiro excéntrico\"" && item.GetProperty("abbreviation").ValueKind == JsonValueKind.Null))
            throw new Exception("Seeded labor abbreviation, category or description missing");
        var laborType = await Send(HttpMethod.Post, "/api/v1/labor-types", new { name = "Labor de prueba", abbreviation = "LAB-PRU" });
        if (laborType.StatusCode != HttpStatusCode.Created) throw new Exception("Labor type create failed");
        var laborTypeRow = await Body(laborType);
        var laborTypeId = laborTypeRow.GetProperty("id").GetGuid();
        if (laborTypeRow.GetProperty("abbreviation").GetString() != "LAB-PRU" || !Absent(laborTypeRow, "category", "description"))
            throw new Exception("Labor abbreviation not returned or old-shape labor has a category");
        if (!await IsError(await Send(HttpMethod.Post, "/api/v1/labor-types", new { name = " disco ", abbreviation = "D" }), HttpStatusCode.Conflict, "Duplicate name") ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/labor-types", new { name = "Labor larga", abbreviation = "ABCDEFGHIJKLM" }), HttpStatusCode.BadRequest) ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/labor-types", new { name = "Labor con categoría", category = "otra" }), HttpStatusCode.BadRequest,
                "Category must be one of: aplicacion, labranza, siembra, cosecha, forrajes, infraestructura") ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/labor-types", new { name = "Labor con categoría", description = new string('x', 301) }), HttpStatusCode.BadRequest,
                "Description must be at most 300 characters") ||
            await Count("labor-types") != 83)
            throw new Exception("Rejected labor changed stored records");
        var changedLabor = await Send(HttpMethod.Put, $"/api/v1/labor-types/{laborTypeId}", new { name = "Labor de prueba", abbreviation = "LP", revision = 1 });
        if (changedLabor.StatusCode != HttpStatusCode.OK || (await Body(changedLabor)).GetProperty("revision").GetInt64() != 2 ||
            (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/labor-types"))!.Single(item => item.GetProperty("id").GetGuid() == laborTypeId)
                .GetProperty("abbreviation").GetString() != "LP")
            throw new Exception("Labor abbreviation did not round-trip");
        if (!await IsError(await Send(HttpMethod.Put, $"/api/v1/labor-types/{laborTypeId}", new { name = "Stale", abbreviation = "S", revision = 1 }), HttpStatusCode.Conflict, "Stale revision"))
            throw new Exception("Stale labor update did not conflict");
        // 010: category and description round-trip; the PUT replaces both.
        var categorizedLabor = await Send(HttpMethod.Post, "/api/v1/labor-types", new { name = "Labor con categoría", category = " Forrajes ", description = "  Descripción ficticia " });
        var categorizedRow = await Body(categorizedLabor);
        var categorizedId = categorizedRow.GetProperty("id").GetGuid();
        if (categorizedLabor.StatusCode != HttpStatusCode.Created || categorizedRow.GetProperty("category").GetString() != "forrajes" ||
            categorizedRow.GetProperty("description").GetString() != "Descripción ficticia" || !Absent(categorizedRow, "abbreviation"))
            throw new Exception($"Labor with category: {(int)categorizedLabor.StatusCode}");
        var laborsBefore = await client.GetStringAsync("/api/v1/labor-types");
        if (!await IsError(await Send(HttpMethod.Put, $"/api/v1/labor-types/{categorizedId}", new { name = "Labor con categoría", category = "aplicación", revision = 1 }),
                HttpStatusCode.BadRequest, "Category must be one of: aplicacion, labranza, siembra, cosecha, forrajes, infraestructura") ||
            await client.GetStringAsync("/api/v1/labor-types") != laborsBefore)
            throw new Exception("Rejected labor category update changed stored records");
        var recategorized = await Send(HttpMethod.Put, $"/api/v1/labor-types/{categorizedId}", new { name = "Labor con categoría", category = "cosecha", revision = 1 });
        if (recategorized.StatusCode != HttpStatusCode.OK || (await Body(recategorized)) is var recategorizedRow &&
            (recategorizedRow.GetProperty("category").GetString() != "cosecha" || !Absent(recategorizedRow, "description") ||
             recategorizedRow.GetProperty("revision").GetInt64() != 2))
            throw new Exception("Labor PUT did not replace category and description");

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
        if (!Absent(contractorRow, "website")) throw new Exception("Old-shape contractor has a website");
        // 010: website round-trip and its validation; the PUT replaces it.
        var contractorsBefore = await client.GetStringAsync("/api/v1/contractors");
        if (!await IsError(await Send(HttpMethod.Post, "/api/v1/contractors", new { name = "Contratista web", website = "www.example .test" }), HttpStatusCode.BadRequest,
                "Website must be a single address without spaces") ||
            !await IsError(await Send(HttpMethod.Put, $"/api/v1/contractors/{contractorId}", new { name = "Contratista de prueba", website = "a b", revision = 1 }),
                HttpStatusCode.BadRequest, "Website must be a single address without spaces") ||
            await client.GetStringAsync("/api/v1/contractors") != contractorsBefore)
            throw new Exception("Rejected website changed stored records");
        var webContractor = await Send(HttpMethod.Put, $"/api/v1/contractors/{contractorId}",
            new { name = "Contratista de prueba", taxId = "20123456789", website = " https://www.example.test ", revision = 1 });
        if (webContractor.StatusCode != HttpStatusCode.OK || (await Body(webContractor)).GetProperty("website").GetString() != "https://www.example.test" ||
            (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/contractors"))!.Single(item => item.GetProperty("id").GetGuid() == contractorId)
                .GetProperty("website").GetString() != "https://www.example.test")
            throw new Exception("Contractor website did not round-trip");
        var changedContractor = await Send(HttpMethod.Put, $"/api/v1/contractors/{contractorId}",
            new { name = "Contratista de prueba", taxId = "20123456789", notes = "Notas", ownEquipment = true, revision = 2 });
        if (changedContractor.StatusCode != HttpStatusCode.OK || (await Body(changedContractor)) is var contractorChange &&
            (contractorChange.GetProperty("notes").GetString() != "Notas" || !contractorChange.GetProperty("ownEquipment").GetBoolean() ||
             !Absent(contractorChange, "website")))
            throw new Exception("Contractor update failed or kept the website");
        if (!await IsError(await Send(HttpMethod.Put, $"/api/v1/contractors/{contractorId}", new { name = "Stale", revision = 1 }), HttpStatusCode.Conflict, "Stale revision") ||
            !await IsError(await Send(HttpMethod.Put, $"/api/v1/contractors/{contractorId}", new { name = "Propio", revision = 3 }), HttpStatusCode.Conflict, "Duplicate name") ||
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
        var inoculant = await Send(HttpMethod.Post, "/api/v1/inputs", new { commercialName = "Inoculante de prueba", unit = "l", category = "Inoculante" });
        if (inoculant.StatusCode != HttpStatusCode.Created || (await Body(inoculant)).GetProperty("category").GetString() != "inoculante")
            throw new Exception("Inoculante input category rejected");

        // 008 work orders: codedPlotId (3 ha) is in richFieldId of client richId, whose firm is firmId; samplePlot is in another client's field.
        // Since 009 an order has no single field: bodies that still send fieldId keep working and it is ignored.
        var rangedInput = await Send(HttpMethod.Post, "/api/v1/inputs", new { commercialName = "Producto con rango", unit = "l", doseMin = 1m, doseMax = 2m });
        if (rangedInput.StatusCode != HttpStatusCode.Created) throw new Exception("Ranged input create failed");
        var rangedInputId = (await Body(rangedInput)).GetProperty("id").GetGuid();
        var orderBody = new { firmId, issuedOn = (string?)"2026-11-24", laborTypeId, stageId = (Guid?)stageId, contractorId, fieldId = richFieldId,
            campaignId = (Guid?)campaignId, cropId = (Guid?)soyId, variety = (string?)"Híbrido ficticio 1", observations = (string?)"Observación ficticia",
            responsibleEngineer = (string?)"Ingeniero ficticio", plots = new object[] { new { plotId = codedPlotId, workedAreaHectares = 3.5m } },
            lines = new object[] { new { inputId = rangedInputId, dosePerHectare = 2.5m }, new { inputId, dosePerHectare = 1m } } };
        async Task<string> OrderState() => await client.GetStringAsync("/api/v1/work-orders") + "|" + await client.GetStringAsync("/api/v1/commercial-firms");
        var withoutCsrf = Guid.NewGuid();
        if ((await client.PostAsJsonAsync("/api/v1/work-orders", orderBody)).StatusCode != HttpStatusCode.Forbidden ||
            (await client.PutAsJsonAsync($"/api/v1/work-orders/{withoutCsrf}", orderBody)).StatusCode != HttpStatusCode.Forbidden ||
            (await client.PostAsJsonAsync($"/api/v1/work-orders/{withoutCsrf}/cancel", new { revision = 1, reason = "Motivo" })).StatusCode != HttpStatusCode.Forbidden ||
            (await client.PostAsJsonAsync($"/api/v1/work-orders/{withoutCsrf}/complete", new { revision = 1 })).StatusCode != HttpStatusCode.Forbidden ||
            await Count("work-orders") != 0)
            throw new Exception("Work order write accepted without CSRF");
        var emptyOrders = await OrderState();
        var samplePlotId = samplePlot.GetProperty("id").GetGuid();
        if (!await IsError(await Send(HttpMethod.Post, "/api/v1/work-orders", orderBody with { plots = new object[] { new { plotId = samplePlotId, workedAreaHectares = 1m } } }),
                HttpStatusCode.BadRequest, "Every plot must belong to the firm's client") ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/work-orders", orderBody with { fieldId = fieldId, plots = new object[] { new { plotId = samplePlotId, workedAreaHectares = 1m } } }),
                HttpStatusCode.BadRequest, "Every plot must belong to the firm's client") ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/work-orders", orderBody with { plots = new object[0] }), HttpStatusCode.BadRequest, "At least one plot is required") ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/work-orders", orderBody with { issuedOn = (string?)null }), HttpStatusCode.BadRequest, "Issued on is required") ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/work-orders", orderBody with { lines = new object[] { new { inputId, dosePerHectare = 0m } } }), HttpStatusCode.BadRequest,
                "Dose must be positive and fit numeric(12,4)") ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/work-orders", orderBody with { plots = new object[] { new { plotId = codedPlotId, workedAreaHectares = 0m } } }),
                HttpStatusCode.BadRequest, "Worked area must be positive and fit numeric(12,4)") ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/work-orders", orderBody with { lines = new object[] { new { inputId = Guid.NewGuid(), dosePerHectare = 1m } } }),
                HttpStatusCode.NotFound, "Record not found") ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/work-orders", orderBody with { firmId = Guid.NewGuid() }), HttpStatusCode.NotFound, "Record not found") ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/work-orders", orderBody with { contractorId = Guid.NewGuid() }), HttpStatusCode.NotFound, "Record not found") ||
            await OrderState() != emptyOrders)
            throw new Exception("Rejected work order create changed stored records or the next number");
        var firmNext = async () => (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/commercial-firms"))!.Single(item => item.GetProperty("id").GetGuid() == firmId);
        if ((await firmNext()).GetProperty("nextWorkOrderNumber").GetInt32() != 1) throw new Exception("Firm next number not exposed");
        var createdOrder = await Send(HttpMethod.Post, "/api/v1/work-orders", orderBody);
        var orderRow = await Body(createdOrder);
        var orderId = orderRow.GetProperty("id").GetGuid();
        var orderPlot = orderRow.GetProperty("plots")[0];
        var orderLines = orderRow.GetProperty("lines");
        if (createdOrder.StatusCode != HttpStatusCode.Created || orderRow.GetProperty("number").GetInt32() != 1 || orderRow.GetProperty("status").GetString() != "emitida" ||
            orderRow.GetProperty("issuedOn").GetString() != "2026-11-24" || orderRow.GetProperty("variety").GetString() != "Híbrido ficticio 1" ||
            orderPlot.GetProperty("areaHectares").GetDecimal() != 3m || orderPlot.GetProperty("workedAreaHectares").GetDecimal() != 3.5m ||
            !orderPlot.GetProperty("workedAreaExceedsPlot").GetBoolean() || orderLines.GetArrayLength() != 2 ||
            orderLines[0].GetProperty("total").GetDecimal() != 8.75m || !orderLines[0].GetProperty("doseOutOfRange").GetBoolean() ||
            orderLines[0].GetProperty("dosePerHectare").GetDecimal() != 2.5m || orderLines[0].GetProperty("unit").GetString() != "l" ||
            orderLines[1].GetProperty("total").GetDecimal() != 3.5m || orderLines[1].GetProperty("doseOutOfRange").GetBoolean() ||
            orderLines[1].GetProperty("unit").GetString() != "kg" || orderLines[1].GetProperty("position").GetInt32() != 2)
            throw new Exception($"Work order create: {(int)createdOrder.StatusCode}");
        var orderDetail = await client.GetAsync($"/api/v1/work-orders/{orderId}");
        var orderSummaries = await client.GetFromJsonAsync<JsonElement[]>("/api/v1/work-orders");
        if (orderDetail.StatusCode != HttpStatusCode.OK || (await Body(orderDetail)).GetProperty("lines").GetArrayLength() != 2 ||
            orderSummaries is not { Length: 1 } || orderSummaries[0].GetProperty("plotCount").GetInt32() != 1 || orderSummaries[0].GetProperty("number").GetInt32() != 1 ||
            orderSummaries[0].GetProperty("firmId").GetGuid() != firmId ||
            !orderSummaries[0].GetProperty("fieldIds").EnumerateArray().Select(item => item.GetGuid()).SequenceEqual([richFieldId]) ||
            (await client.GetAsync($"/api/v1/work-orders/{Guid.NewGuid()}")).StatusCode != HttpStatusCode.NotFound ||
            (await firmNext()).GetProperty("nextWorkOrderNumber").GetInt32() != 2)
            throw new Exception("Work order detail or list mismatch");

        // The firm PUT sets the next number only when sent; it must exceed every issued number.
        var firmNow = await firmNext();
        var firmRevision = firmNow.GetProperty("revision").GetInt64();
        var firmName = firmNow.GetProperty("name").GetString();
        var issuedOrders = await OrderState();
        if (!await IsError(await Send(HttpMethod.Put, $"/api/v1/commercial-firms/{firmId}", new { name = firmName, revision = firmRevision, nextWorkOrderNumber = 1 }),
                HttpStatusCode.BadRequest, "Next number must be greater than 1") ||
            await OrderState() != issuedOrders)
            throw new Exception("Next number onto an issued number accepted");
        var renumberedFirm = await Send(HttpMethod.Put, $"/api/v1/commercial-firms/{firmId}", new { name = firmName, revision = firmRevision, nextWorkOrderNumber = 50 });
        if (renumberedFirm.StatusCode != HttpStatusCode.OK || (await Body(renumberedFirm)).GetProperty("nextWorkOrderNumber").GetInt32() != 50)
            throw new Exception("Next number update failed");
        var keptNumber = await Send(HttpMethod.Put, $"/api/v1/commercial-firms/{firmId}", new { name = firmName, revision = firmRevision + 1 });
        if (keptNumber.StatusCode != HttpStatusCode.OK || (await Body(keptNumber)).GetProperty("nextWorkOrderNumber").GetInt32() != 50)
            throw new Exception("Omitted next number changed it");
        var laterOrder = await Send(HttpMethod.Post, "/api/v1/work-orders", orderBody with { lines = new object[0] });
        var laterRow = await Body(laterOrder);
        var laterId = laterRow.GetProperty("id").GetGuid();
        if (laterOrder.StatusCode != HttpStatusCode.Created || laterRow.GetProperty("number").GetInt32() != 50 || laterRow.GetProperty("lines").GetArrayLength() != 0)
            throw new Exception("Custom next number not honored");

        // Update is a full replace of header, plots and lines; firm and number stay.
        var editBody = orderBody with { stageId = (Guid?)null, variety = (string?)null, plots = new object[] { new { plotId = codedPlotId, workedAreaHectares = 2m } }, lines = new object[0] };
        issuedOrders = await OrderState();
        var orderDetailBefore = await client.GetStringAsync($"/api/v1/work-orders/{orderId}");
        if (!await IsError(await Send(HttpMethod.Put, $"/api/v1/work-orders/{orderId}", new { editBody.issuedOn, editBody.laborTypeId, editBody.contractorId,
                editBody.fieldId, editBody.plots, editBody.lines, revision = 2 }), HttpStatusCode.Conflict, "Stale revision") ||
            !await IsError(await Send(HttpMethod.Put, $"/api/v1/work-orders/{orderId}", new { editBody.issuedOn, editBody.laborTypeId, editBody.contractorId,
                editBody.fieldId, plots = new object[] { new { plotId = samplePlotId, workedAreaHectares = 1m } }, revision = 1 }), HttpStatusCode.BadRequest,
                "Every plot must belong to the firm's client") ||
            !await IsError(await Send(HttpMethod.Put, $"/api/v1/work-orders/{orderId}", new { editBody.issuedOn, laborTypeId = Guid.NewGuid(), editBody.contractorId,
                editBody.fieldId, editBody.plots, revision = 1 }), HttpStatusCode.NotFound, "Record not found") ||
            !await IsError(await Send(HttpMethod.Put, $"/api/v1/work-orders/{Guid.NewGuid()}", new { editBody.issuedOn, editBody.laborTypeId, editBody.contractorId,
                editBody.fieldId, editBody.plots, revision = 1 }), HttpStatusCode.NotFound, "Record not found") ||
            await OrderState() != issuedOrders || await client.GetStringAsync($"/api/v1/work-orders/{orderId}") != orderDetailBefore)
            throw new Exception("Rejected work order update changed stored records");
        var editedOrder = await Send(HttpMethod.Put, $"/api/v1/work-orders/{orderId}", new { firmId = Guid.NewGuid(), number = 99, editBody.issuedOn, editBody.laborTypeId,
            editBody.contractorId, editBody.fieldId, editBody.campaignId, editBody.cropId, editBody.observations, editBody.responsibleEngineer, editBody.plots, editBody.lines, revision = 1 });
        var editedRow = await Body(editedOrder);
        if (editedOrder.StatusCode != HttpStatusCode.OK || editedRow.GetProperty("number").GetInt32() != 1 || editedRow.GetProperty("firmId").GetGuid() != firmId ||
            editedRow.GetProperty("revision").GetInt64() != 2 || editedRow.GetProperty("stageId").ValueKind != JsonValueKind.Null || !Absent(editedRow, "variety") ||
            editedRow.GetProperty("lines").GetArrayLength() != 0 || editedRow.GetProperty("plots")[0].GetProperty("workedAreaExceedsPlot").GetBoolean())
            throw new Exception($"Work order update: {(int)editedOrder.StatusCode}");

        // Filters by firm and status.
        if ((await Ids($"work-orders?firmId={firmId}")).Length != 2 || (await Ids($"work-orders?firmId={Guid.NewGuid()}")).Length != 0 ||
            (await Ids("work-orders?status=emitida")).Length != 2 || (await Ids($"work-orders?firmId={firmId}&status=cancelada")).Length != 0 ||
            !await IsError(await client.GetAsync("/api/v1/work-orders?status=anulada"), HttpStatusCode.BadRequest, "Status must be one of: emitida, realizada, cancelada"))
            throw new Exception("Work order filters mismatch");

        // Cancel keeps the number and the order stays queryable; closed orders are not editable.
        issuedOrders = await OrderState();
        if (!await IsError(await Send(HttpMethod.Post, $"/api/v1/work-orders/{laterId}/cancel", new { revision = 1, reason = " " }), HttpStatusCode.BadRequest, "Reason is required") ||
            !await IsError(await Send(HttpMethod.Post, $"/api/v1/work-orders/{laterId}/cancel", new { revision = 2, reason = "Lluvia" }), HttpStatusCode.Conflict, "Stale revision") ||
            !await IsError(await Send(HttpMethod.Post, $"/api/v1/work-orders/{Guid.NewGuid()}/cancel", new { revision = 1, reason = "Lluvia" }), HttpStatusCode.NotFound, "Record not found") ||
            !await IsError(await Send(HttpMethod.Post, $"/api/v1/work-orders/{Guid.NewGuid()}/complete", new { revision = 1 }), HttpStatusCode.NotFound, "Record not found") ||
            !await IsError(await Send(HttpMethod.Post, $"/api/v1/work-orders/{orderId}/complete", new { revision = 1 }), HttpStatusCode.Conflict, "Stale revision") ||
            await OrderState() != issuedOrders)
            throw new Exception("Rejected cancel or complete changed stored records");
        var cancelledOrder = await Send(HttpMethod.Post, $"/api/v1/work-orders/{laterId}/cancel", new { revision = 1, reason = "Lluvia" });
        var cancelledRow = await Body(cancelledOrder);
        if (cancelledOrder.StatusCode != HttpStatusCode.OK || cancelledRow.GetProperty("status").GetString() != "cancelada" ||
            cancelledRow.GetProperty("cancelReason").GetString() != "Lluvia" || cancelledRow.GetProperty("number").GetInt32() != 50 ||
            (await client.GetFromJsonAsync<JsonElement>($"/api/v1/work-orders/{laterId}")).GetProperty("status").GetString() != "cancelada" ||
            !(await Ids("work-orders?status=cancelada")).SequenceEqual([laterId]))
            throw new Exception("Cancel failed or hid the work order");
        var completedOrder = await Send(HttpMethod.Post, $"/api/v1/work-orders/{orderId}/complete", new { revision = 2 });
        if (completedOrder.StatusCode != HttpStatusCode.OK || (await Body(completedOrder)).GetProperty("status").GetString() != "realizada")
            throw new Exception("Complete failed");
        issuedOrders = await OrderState();
        foreach (var (closedId, closedRevision) in new[] { (laterId, 2L), (orderId, 3L) })
            if (!await IsError(await Send(HttpMethod.Put, $"/api/v1/work-orders/{closedId}", new { editBody.issuedOn, editBody.laborTypeId, editBody.contractorId,
                    editBody.fieldId, editBody.plots, revision = closedRevision }), HttpStatusCode.Conflict, "Work order is not editable") ||
                !await IsError(await Send(HttpMethod.Post, $"/api/v1/work-orders/{closedId}/cancel", new { revision = closedRevision, reason = "Motivo" }), HttpStatusCode.Conflict, "Work order is not editable") ||
                !await IsError(await Send(HttpMethod.Post, $"/api/v1/work-orders/{closedId}/complete", new { revision = closedRevision }), HttpStatusCode.Conflict, "Work order is not editable"))
                throw new Exception("Closed work order accepted a write");
        if (await OrderState() != issuedOrders) throw new Exception("Rejected writes to closed work orders changed stored records");
        var afterCancel = await Send(HttpMethod.Post, "/api/v1/work-orders", orderBody);
        if (afterCancel.StatusCode != HttpStatusCode.Created || (await Body(afterCancel)).GetProperty("number").GetInt32() != 51 || await Count("work-orders") != 3)
            throw new Exception("Create after cancel did not take the following number");

        // 009: one order across two fields of the firm's client, without fieldId in the body.
        var northField = await Body(await Send(HttpMethod.Post, "/api/v1/fields", new { clientId = richId, name = "Campo norte" }));
        var northFieldId = northField.GetProperty("id").GetGuid();
        var northPlot = await Send(HttpMethod.Post, "/api/v1/plots", new { fieldId = northFieldId, name = "Lote N", areaHectares = 2m, code = "NOR-1" });
        var northPlotId = (await Body(northPlot)).GetProperty("id").GetGuid();
        var crossBody = new { firmId, issuedOn = "2026-11-24", laborTypeId, stageId, contractorId, campaignId, cropId = soyId, variety = "Híbrido ficticio 1",
            observations = "Observación ficticia", responsibleEngineer = "Ingeniero ficticio",
            plots = new object[] { new { plotId = codedPlotId, workedAreaHectares = 3.5m }, new { plotId = northPlotId, workedAreaHectares = 2m } },
            lines = new object[] { new { inputId = rangedInputId, dosePerHectare = 2.5m } } };
        issuedOrders = await OrderState();
        if (!await IsError(await Send(HttpMethod.Post, "/api/v1/work-orders", crossBody with { plots = new object[] {
                new { plotId = codedPlotId, workedAreaHectares = 1m }, new { plotId = samplePlotId, workedAreaHectares = 1m } } }),
                HttpStatusCode.BadRequest, "Every plot must belong to the firm's client") ||
            await OrderState() != issuedOrders)
            throw new Exception("Rejected cross-field order changed stored records or the next number");
        var crossOrder = await Send(HttpMethod.Post, "/api/v1/work-orders", crossBody);
        var crossRow = await Body(crossOrder);
        var crossId = crossRow.GetProperty("id").GetGuid();
        if (crossOrder.StatusCode != HttpStatusCode.Created || crossRow.GetProperty("number").GetInt32() != 52 || crossRow.GetProperty("plots").GetArrayLength() != 2 ||
            crossRow.GetProperty("lines")[0].GetProperty("total").GetDecimal() != 13.75m || crossRow.TryGetProperty("fieldId", out _) ||
            !crossRow.GetProperty("plots").EnumerateArray().Select(item => item.GetProperty("fieldId").GetGuid()).Order().SequenceEqual(new[] { richFieldId, northFieldId }.Order()))
            throw new Exception($"Cross-field work order: {(int)crossOrder.StatusCode}");
        var crossSummary = (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/work-orders"))!.Single(item => item.GetProperty("id").GetGuid() == crossId);
        if (crossSummary.GetProperty("plotCount").GetInt32() != 2 ||
            !crossSummary.GetProperty("fieldIds").EnumerateArray().Select(item => item.GetGuid()).SequenceEqual([richFieldId, northFieldId]))
            throw new Exception("Cross-field summary field ids mismatch");

        // Search by number, firm, field, plot name or code; combinable with firm and status; blank is no filter.
        var everyOrder = await Ids("work-orders");
        if (!(await Ids("work-orders?q=52")).SequenceEqual([crossId]) || !(await Ids("work-orders?q=campo%20NORTE")).SequenceEqual([crossId]) ||
            !(await Ids("work-orders?q=lote%20n")).SequenceEqual([crossId]) || !(await Ids("work-orders?q=nor-1")).SequenceEqual([crossId]) ||
            !(await Ids("work-orders?q=FIRMA%20comercial")).SequenceEqual(everyOrder) || !(await Ids("work-orders?q=%20%20")).SequenceEqual(everyOrder) ||
            (await Ids("work-orders?q=inexistente")).Length != 0 || (await Ids($"work-orders?firmId={Guid.NewGuid()}&q=52")).Length != 0 ||
            !(await Ids("work-orders?status=cancelada&q=firma")).SequenceEqual([laterId]) ||
            !await IsError(await client.GetAsync("/api/v1/work-orders?status=anulada&q=52"), HttpStatusCode.BadRequest, "Status must be one of: emitida, realizada, cancelada"))
            throw new Exception("Work order search mismatch");

        // Print document with display values resolved server-side and the workbook file name.
        if ((await client.GetAsync($"/api/v1/work-orders/{Guid.NewGuid()}/document")).StatusCode != HttpStatusCode.NotFound)
            throw new Exception("Document of a missing work order");
        var documentResponse = await client.GetAsync($"/api/v1/work-orders/{crossId}/document");
        var document = await Body(documentResponse);
        var documentPlots = document.GetProperty("plots");
        var documentLine = document.GetProperty("lines").EnumerateArray().Single();
        if (documentResponse.StatusCode != HttpStatusCode.OK || document.GetProperty("number").GetInt32() != 52 || document.GetProperty("issuedOn").GetString() != "2026-11-24" ||
            document.GetProperty("status").GetString() != "emitida" || !Absent(document, "cancelReason") ||
            document.GetProperty("firm").GetProperty("name").GetString() != "Firma comercial SA" || !Absent(document.GetProperty("firm"), "taxId") ||
            document.GetProperty("clientName").GetString() != "Cliente con datos" ||
            document.GetProperty("labor").GetProperty("name").GetString() != "Labor de prueba" || document.GetProperty("labor").GetProperty("abbreviation").GetString() != "LP" ||
            document.GetProperty("stageName").GetString() != "Cosecha" || document.GetProperty("campaignName").GetString() != "2026-27" ||
            document.GetProperty("cropName").GetString() != "Soja" || document.GetProperty("variety").GetString() != "Híbrido ficticio 1" ||
            document.GetProperty("contractor").GetProperty("name").GetString() != "Contratista de prueba" || !document.GetProperty("contractor").GetProperty("ownEquipment").GetBoolean() ||
            documentPlots.GetArrayLength() != 2 || documentPlots[0].GetProperty("fieldName").GetString() != "Campo alquilado" ||
            documentPlots[0].GetProperty("plotName").GetString() != "Lote" || !Absent(documentPlots[0], "plotCode") ||
            documentPlots[0].GetProperty("areaHectares").GetDecimal() != 3m || documentPlots[0].GetProperty("workedAreaHectares").GetDecimal() != 3.5m ||
            !documentPlots[0].GetProperty("workedAreaExceedsPlot").GetBoolean() || documentPlots[1].GetProperty("fieldName").GetString() != "Campo norte" ||
            documentPlots[1].GetProperty("plotCode").GetString() != "NOR-1" || documentPlots[1].GetProperty("workedAreaExceedsPlot").GetBoolean() ||
            document.GetProperty("totalWorkedAreaHectares").GetDecimal() != 5.5m ||
            documentLine.GetProperty("position").GetInt32() != 1 || documentLine.GetProperty("inputName").GetString() != "Producto con rango" ||
            !Absent(documentLine, "formulation", "mixOrder") || documentLine.GetProperty("dosePerHectare").GetDecimal() != 2.5m ||
            documentLine.GetProperty("unit").GetString() != "l" || documentLine.GetProperty("total").GetDecimal() != 13.75m ||
            !documentLine.GetProperty("doseOutOfRange").GetBoolean() || documentLine.GetProperty("hollowCone").GetBoolean() ||
            document.GetProperty("observations").GetString() != "Observación ficticia" || document.GetProperty("responsibleEngineer").GetString() != "Ingeniero ficticio" ||
            document.GetProperty("fileName").GetString() != "FIRMA COMERCIAL SA_ORDEN_52_CAMPO ALQUILADO_(Lote)_CAMPO NORTE_(Lote N)_LP_24-nov")
            throw new Exception($"Work order document: {(int)documentResponse.StatusCode} {document}");
        // 010: a field moves to another client and a plot to another field via PUT; omitted targets keep the current ones.
        var mobileField = await Body(await Send(HttpMethod.Post, "/api/v1/fields", new { clientId = id, name = "Campo móvil" }));
        var mobileFieldId = mobileField.GetProperty("id").GetGuid();
        var mobilePlotId = (await Body(await Send(HttpMethod.Post, "/api/v1/plots", new { fieldId = mobileFieldId, name = "Lote móvil", areaHectares = 1m }))).GetProperty("id").GetGuid();
        async Task<string> Hierarchy() => await client.GetStringAsync("/api/v1/fields") + "|" + await client.GetStringAsync("/api/v1/plots") + "|" + await OrderState();
        var hierarchyBefore = await Hierarchy();
        if (!await IsError(await Send(HttpMethod.Put, $"/api/v1/fields/{mobileFieldId}", new { name = "Campo móvil", clientId = Guid.NewGuid(), revision = 1 }), HttpStatusCode.NotFound, "Record not found") ||
            !await IsError(await Send(HttpMethod.Put, $"/api/v1/plots/{mobilePlotId}", new { name = "Lote móvil", areaHectares = 1m, fieldId = Guid.NewGuid(), revision = 1 }), HttpStatusCode.NotFound, "Record not found") ||
            !await IsError(await Send(HttpMethod.Put, $"/api/v1/fields/{mobileFieldId}", new { name = "Campo móvil", clientId = richId, revision = 2 }), HttpStatusCode.Conflict, "Stale revision") ||
            await Hierarchy() != hierarchyBefore)
            throw new Exception("Rejected moves changed stored records");
        var movedField = await Send(HttpMethod.Put, $"/api/v1/fields/{mobileFieldId}", new { name = "Campo móvil", clientId = richId, revision = 1 });
        var keptClientField = await Send(HttpMethod.Put, $"/api/v1/fields/{mobileFieldId}", new { name = "Campo móvil", locality = "Localidad ficticia", revision = 2 });
        if (movedField.StatusCode != HttpStatusCode.OK || (await Body(movedField)).GetProperty("clientId").GetGuid() != richId ||
            keptClientField.StatusCode != HttpStatusCode.OK || (await Body(keptClientField)).GetProperty("clientId").GetGuid() != richId ||
            !(await Ids($"fields?clientId={richId}")).Contains(mobileFieldId) || (await Ids($"fields?clientId={id}")).Contains(mobileFieldId))
            throw new Exception("Field move via PUT failed");
        var movedPlot = await Send(HttpMethod.Put, $"/api/v1/plots/{mobilePlotId}", new { name = "Lote móvil", areaHectares = 1m, fieldId = northFieldId, revision = 1 });
        var keptFieldPlot = await Send(HttpMethod.Put, $"/api/v1/plots/{mobilePlotId}", new { name = "Lote móvil", areaHectares = 1.5m, revision = 2 });
        if (movedPlot.StatusCode != HttpStatusCode.OK || (await Body(movedPlot)).GetProperty("fieldId").GetGuid() != northFieldId ||
            keptFieldPlot.StatusCode != HttpStatusCode.OK || (await Body(keptFieldPlot)).GetProperty("fieldId").GetGuid() != northFieldId ||
            !(await Ids($"plots?fieldId={northFieldId}")).Contains(mobilePlotId) || (await Ids($"plots?fieldId={mobileFieldId}")).Length != 0)
            throw new Exception("Plot move via PUT failed");
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
        // 011: providers, contacts, client links, reference prices and quote comparisons.
        var unknownId = Guid.NewGuid();
        string[] providerReads = ["providers", "providers?kind=insumos", "provider-contacts", $"provider-contacts?providerId={unknownId}", "client-providers",
            $"client-providers?clientId={unknownId}&providerId={unknownId}", "provider-prices", $"provider-prices?providerId={unknownId}&inputId={unknownId}", "quote-comparisons",
            $"quote-comparisons?clientId={unknownId}&status=abierta", $"quote-comparisons/{unknownId}"];
        (HttpMethod Method, string Path)[] providerWrites = [(HttpMethod.Post, "providers"), (HttpMethod.Put, $"providers/{unknownId}"), (HttpMethod.Post, "provider-contacts"),
            (HttpMethod.Put, $"provider-contacts/{unknownId}"), (HttpMethod.Post, "client-providers"), (HttpMethod.Delete, $"client-providers/{unknownId}"),
            (HttpMethod.Post, "provider-prices"), (HttpMethod.Put, $"provider-prices/{unknownId}"), (HttpMethod.Post, "quote-comparisons"),
            (HttpMethod.Put, $"quote-comparisons/{unknownId}"), (HttpMethod.Post, $"quote-comparisons/{unknownId}/decide"), (HttpMethod.Post, $"quote-comparisons/{unknownId}/reopen"),
            (HttpMethod.Post, $"quote-comparisons/{unknownId}/quotes"), (HttpMethod.Put, $"quotes/{unknownId}")];
        async Task<string> ProviderState()
        {
            var state = new List<string>();
            foreach (var route in new[] { "providers", "provider-contacts", "client-providers", "provider-prices", "quote-comparisons" })
                state.Add(await client.GetStringAsync($"/api/v1/{route}"));
            foreach (var comparisonRow in (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/quote-comparisons"))!)
                state.Add(await client.GetStringAsync($"/api/v1/quote-comparisons/{comparisonRow.GetProperty("id").GetGuid()}"));
            return string.Join("|", state);
        }
        using (var anonymous = new HttpClient { BaseAddress = client.BaseAddress })
        {
            foreach (var route in providerReads)
                if ((await anonymous.GetAsync($"/api/v1/{route}")).StatusCode != HttpStatusCode.Unauthorized)
                    throw new Exception($"Anonymous GET {route} accessible");
            foreach (var (method, path) in providerWrites)
                using (var request = new HttpRequestMessage(method, $"/api/v1/{path}") { Content = JsonContent.Create(new { name = "Example", revision = 1 }) })
                    if ((await anonymous.SendAsync(request)).StatusCode != HttpStatusCode.Unauthorized)
                        throw new Exception($"Anonymous {method} {path} accessible");
        }
        var providerBody = new { name = "Proveedor de prueba", kind = "insumos", taxId = (string?)"30-12345678-9", website = (string?)"https://www.example.test",
            phone = (string?)"0000 000000", email = (string?)"ventas@example.test", notes = (string?)null };
        var emptyProviders = await ProviderState();
        foreach (var (method, path) in providerWrites)
            using (var request = new HttpRequestMessage(method, $"/api/v1/{path}") { Content = JsonContent.Create(providerBody) })
                if ((await client.SendAsync(request)).StatusCode != HttpStatusCode.Forbidden)
                    throw new Exception($"{method} {path} accepted without CSRF");
        if (await ProviderState() != emptyProviders || (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/providers"))!.Length != 0)
            throw new Exception("Provider writes without CSRF changed stored records");
        const string kindError = "Kind must be one of: insumos, laboratorio_suelos, laboratorio_semillas, plataforma_virtual, otro";
        if (!await IsError(await Send(HttpMethod.Post, "/api/v1/providers", providerBody with { kind = "contratista" }), HttpStatusCode.BadRequest, kindError) ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/providers", providerBody with { taxId = "123" }), HttpStatusCode.BadRequest, "CUIT must have 11 digits") ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/providers", providerBody with { website = "a b" }), HttpStatusCode.BadRequest, "Website must be a single address without spaces") ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/providers", providerBody with { email = "a@b@c" }), HttpStatusCode.BadRequest) ||
            !await IsError(await client.GetAsync("/api/v1/providers?kind=laboratorio"), HttpStatusCode.BadRequest, kindError) ||
            await ProviderState() != emptyProviders)
            throw new Exception("Rejected providers changed stored records");
        var providerResponse = await Send(HttpMethod.Post, "/api/v1/providers", providerBody);
        var providerRow = await Body(providerResponse);
        var providerId = providerRow.GetProperty("id").GetGuid();
        if (providerResponse.StatusCode != HttpStatusCode.Created || providerRow.GetProperty("kind").GetString() != "insumos" ||
            providerRow.GetProperty("taxId").GetString() != "30123456789" || providerRow.GetProperty("website").GetString() != "https://www.example.test" ||
            providerRow.GetProperty("revision").GetInt64() != 1 || !Absent(providerRow, "notes"))
            throw new Exception($"Provider create: {(int)providerResponse.StatusCode} {providerRow}");
        var labRow = await Body(await Send(HttpMethod.Post, "/api/v1/providers", new { name = "Laboratorio de prueba", kind = "laboratorio_suelos" }));
        var labId = labRow.GetProperty("id").GetGuid();
        var providersBefore = await ProviderState();
        if (!await IsError(await Send(HttpMethod.Post, "/api/v1/providers", providerBody with { name = " PROVEEDOR DE PRUEBA " }), HttpStatusCode.Conflict, "Duplicate name") ||
            !await IsError(await Send(HttpMethod.Put, $"/api/v1/providers/{labId}", new { name = "proveedor de prueba", kind = "otro", revision = 1 }), HttpStatusCode.Conflict, "Duplicate name") ||
            !await IsError(await Send(HttpMethod.Put, $"/api/v1/providers/{providerId}", new { name = "Proveedor de prueba", kind = "x", revision = 1 }), HttpStatusCode.BadRequest, kindError) ||
            !await IsError(await Send(HttpMethod.Put, $"/api/v1/providers/{providerId}", new { name = "Stale", kind = "insumos", revision = 0 }), HttpStatusCode.Conflict, "Stale revision") ||
            !await IsError(await Send(HttpMethod.Put, $"/api/v1/providers/{Guid.NewGuid()}", new { name = "Ausente", kind = "insumos", revision = 1 }), HttpStatusCode.NotFound, "Record not found") ||
            await ProviderState() != providersBefore)
            throw new Exception("Rejected provider updates changed stored records");
        var changedLab = await Send(HttpMethod.Put, $"/api/v1/providers/{labId}", new { name = "Laboratorio de prueba", kind = "laboratorio_semillas", website = "www.lab.example.test", revision = 1 });
        if (changedLab.StatusCode != HttpStatusCode.OK || (await Body(changedLab)) is var changedLabRow && (changedLabRow.GetProperty("kind").GetString() != "laboratorio_semillas" ||
            changedLabRow.GetProperty("website").GetString() != "www.lab.example.test" || changedLabRow.GetProperty("revision").GetInt64() != 2))
            throw new Exception("Provider update failed");
        if (!(await Ids("providers")).SequenceEqual([labId, providerId]) || !(await Ids("providers?kind=insumos")).SequenceEqual([providerId]) ||
            !(await Ids("providers?kind=laboratorio_semillas")).SequenceEqual([labId]) || (await Ids("providers?kind=otro")).Length != 0)
            throw new Exception("Providers filter by kind mismatch");

        var contactBody = new { providerId, name = "Representante", role = "ventas", phone = (string?)"0000 111111", email = (string?)"rep@example.test", notes = (string?)null };
        providersBefore = await ProviderState();
        if (!await IsError(await Send(HttpMethod.Post, "/api/v1/provider-contacts", contactBody with { role = "comercial" }), HttpStatusCode.BadRequest,
                "Role must be one of: ventas, tecnico, otro") ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/provider-contacts", contactBody with { providerId = Guid.NewGuid() }), HttpStatusCode.NotFound, "Record not found") ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/provider-contacts", contactBody with { email = "a@b@c" }), HttpStatusCode.BadRequest) ||
            await ProviderState() != providersBefore)
            throw new Exception("Rejected contacts changed stored records");
        var contactResponse = await Send(HttpMethod.Post, "/api/v1/provider-contacts", contactBody);
        var contactRow = await Body(contactResponse);
        var contactId = contactRow.GetProperty("id").GetGuid();
        if (contactResponse.StatusCode != HttpStatusCode.Created || contactRow.GetProperty("providerId").GetGuid() != providerId ||
            contactRow.GetProperty("role").GetString() != "ventas" || contactRow.GetProperty("email").GetString() != "rep@example.test")
            throw new Exception($"Contact create: {(int)contactResponse.StatusCode}");
        var labContactId = (await Body(await Send(HttpMethod.Post, "/api/v1/provider-contacts", new { providerId = labId, name = "Técnico", role = "tecnico" }))).GetProperty("id").GetGuid();
        // The update body has no provider: a contact never moves to another provider, even if one is sent.
        var changedContact = await Send(HttpMethod.Put, $"/api/v1/provider-contacts/{contactId}", new { providerId = labId, name = "Representante", role = "otro", revision = 1 });
        if (changedContact.StatusCode != HttpStatusCode.OK || (await Body(changedContact)) is var changedContactRow &&
            (changedContactRow.GetProperty("providerId").GetGuid() != providerId || changedContactRow.GetProperty("role").GetString() != "otro" ||
             !Absent(changedContactRow, "phone", "email", "notes") || changedContactRow.GetProperty("revision").GetInt64() != 2))
            throw new Exception("Contact update failed or moved the contact");
        providersBefore = await ProviderState();
        if (!await IsError(await Send(HttpMethod.Put, $"/api/v1/provider-contacts/{contactId}", new { name = "Stale", role = "ventas", revision = 1 }), HttpStatusCode.Conflict, "Stale revision") ||
            !await IsError(await Send(HttpMethod.Put, $"/api/v1/provider-contacts/{contactId}", new { name = "Representante", role = "x", revision = 2 }), HttpStatusCode.BadRequest,
                "Role must be one of: ventas, tecnico, otro") ||
            !await IsError(await Send(HttpMethod.Put, $"/api/v1/provider-contacts/{Guid.NewGuid()}", new { name = "Ausente", role = "ventas", revision = 1 }), HttpStatusCode.NotFound) ||
            await ProviderState() != providersBefore)
            throw new Exception("Rejected contact updates changed stored records");
        if (!(await Ids($"provider-contacts?providerId={providerId}")).SequenceEqual([contactId]) || !(await Ids($"provider-contacts?providerId={labId}")).SequenceEqual([labContactId]) ||
            (await Ids("provider-contacts")).Length != 2)
            throw new Exception("Contacts filter by provider mismatch");

        providersBefore = await ProviderState();
        if (!await IsError(await Send(HttpMethod.Post, "/api/v1/client-providers", new { clientId = Guid.NewGuid(), providerId }), HttpStatusCode.NotFound, "Record not found") ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/client-providers", new { clientId = id, providerId = Guid.NewGuid() }), HttpStatusCode.NotFound, "Record not found") ||
            await ProviderState() != providersBefore)
            throw new Exception("Rejected links changed stored records");
        var linkResponse = await Send(HttpMethod.Post, "/api/v1/client-providers", new { clientId = id, providerId });
        var linkRow = await Body(linkResponse);
        var linkId = linkRow.GetProperty("id").GetGuid();
        if (linkResponse.StatusCode != HttpStatusCode.Created || linkRow.GetProperty("clientId").GetGuid() != id || linkRow.GetProperty("providerId").GetGuid() != providerId)
            throw new Exception($"Link create: {(int)linkResponse.StatusCode}");
        var richLinkId = (await Body(await Send(HttpMethod.Post, "/api/v1/client-providers", new { clientId = richId, providerId }))).GetProperty("id").GetGuid();
        var labLinkId = (await Body(await Send(HttpMethod.Post, "/api/v1/client-providers", new { clientId = id, providerId = labId }))).GetProperty("id").GetGuid();
        providersBefore = await ProviderState();
        if (!await IsError(await Send(HttpMethod.Post, "/api/v1/client-providers", new { clientId = id, providerId }), HttpStatusCode.Conflict, "Duplicate link") ||
            await ProviderState() != providersBefore)
            throw new Exception("Duplicate link changed stored records");
        if ((await Ids($"client-providers?clientId={id}")).Order().SequenceEqual(new[] { linkId, labLinkId }.Order()) is false ||
            !(await Ids($"client-providers?providerId={labId}")).SequenceEqual([labLinkId]) ||
            !(await Ids($"client-providers?clientId={richId}&providerId={providerId}")).SequenceEqual([richLinkId]) || (await Ids("client-providers")).Length != 3)
            throw new Exception("Links filter mismatch");
        var unlinked = await Send(HttpMethod.Delete, $"/api/v1/client-providers/{labLinkId}", new { });
        if (unlinked.StatusCode != HttpStatusCode.NoContent || (await unlinked.Content.ReadAsStringAsync()).Length != 0 ||
            !await IsError(await Send(HttpMethod.Delete, $"/api/v1/client-providers/{labLinkId}", new { }), HttpStatusCode.NotFound, "Record not found") ||
            (await Ids("client-providers")).Length != 2 || (await Ids("providers")).Length != 2)
            throw new Exception("Link delete failed or removed more than the link");

        var priceBody = new { providerId, inputId, price = 12.5m, currency = "USD", unit = (string?)null, validOn = (string?)"2026-11-01", notes = (string?)null };
        providersBefore = await ProviderState();
        if (!await IsError(await Send(HttpMethod.Post, "/api/v1/provider-prices", priceBody with { price = 0m }), HttpStatusCode.BadRequest, "Price must be positive and fit numeric(14,4)") ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/provider-prices", priceBody with { currency = "EUR" }), HttpStatusCode.BadRequest, "Currency must be ARS or USD") ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/provider-prices", priceBody with { validOn = null }), HttpStatusCode.BadRequest, "Valid on is required") ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/provider-prices", priceBody with { inputId = Guid.NewGuid() }), HttpStatusCode.NotFound, "Record not found") ||
            await ProviderState() != providersBefore)
            throw new Exception("Rejected prices changed stored records");
        var priceResponse = await Send(HttpMethod.Post, "/api/v1/provider-prices", priceBody);
        var priceRow = await Body(priceResponse);
        var priceId = priceRow.GetProperty("id").GetGuid();
        if (priceResponse.StatusCode != HttpStatusCode.Created || priceRow.GetProperty("unit").GetString() != "kg" || priceRow.GetProperty("price").GetDecimal() != 12.5m ||
            priceRow.GetProperty("currency").GetString() != "USD" || priceRow.GetProperty("validOn").GetString() != "2026-11-01" || priceRow.GetProperty("inputId").GetGuid() != inputId)
            throw new Exception($"Price create: {(int)priceResponse.StatusCode} {priceRow}");
        var rangedPriceId = (await Body(await Send(HttpMethod.Post, "/api/v1/provider-prices",
            priceBody with { inputId = rangedInputId, currency = "ARS", unit = "bidon", validOn = "2026-11-20" }))).GetProperty("id").GetGuid();
        var changedPrice = await Send(HttpMethod.Put, $"/api/v1/provider-prices/{priceId}", new { inputId = rangedInputId, price = 13m, currency = "ARS", validOn = "2026-11-02", revision = 1 });
        if (changedPrice.StatusCode != HttpStatusCode.OK || (await Body(changedPrice)) is var changedPriceRow && (changedPriceRow.GetProperty("unit").GetString() != "l" ||
            changedPriceRow.GetProperty("providerId").GetGuid() != providerId || changedPriceRow.GetProperty("inputId").GetGuid() != rangedInputId ||
            changedPriceRow.GetProperty("revision").GetInt64() != 2))
            throw new Exception("Price update failed");
        providersBefore = await ProviderState();
        if (!await IsError(await Send(HttpMethod.Put, $"/api/v1/provider-prices/{priceId}", new { price = 1m, currency = "ARS", validOn = "2026-11-02", revision = 1 }), HttpStatusCode.Conflict, "Stale revision") ||
            !await IsError(await Send(HttpMethod.Put, $"/api/v1/provider-prices/{priceId}", new { price = -1m, currency = "ARS", validOn = "2026-11-02", revision = 2 }), HttpStatusCode.BadRequest) ||
            !await IsError(await Send(HttpMethod.Put, $"/api/v1/provider-prices/{Guid.NewGuid()}", new { price = 1m, currency = "ARS", validOn = "2026-11-02", revision = 1 }), HttpStatusCode.NotFound) ||
            await ProviderState() != providersBefore)
            throw new Exception("Rejected price updates changed stored records");
        if (!(await Ids($"provider-prices?inputId={rangedInputId}")).SequenceEqual([rangedPriceId, priceId]) || (await Ids($"provider-prices?inputId={inputId}")).Length != 0 ||
            !(await Ids($"provider-prices?providerId={providerId}&inputId={rangedInputId}")).SequenceEqual([rangedPriceId, priceId]) ||
            (await Ids($"provider-prices?providerId={labId}")).Length != 0)
            throw new Exception("Prices filter mismatch");

        providersBefore = await ProviderState();
        if (!await IsError(await Send(HttpMethod.Post, "/api/v1/quote-comparisons", new { clientId = id, title = " ", requestedOn = "2026-11-10" }), HttpStatusCode.BadRequest, "Title is required") ||
            !await IsError(await Send(HttpMethod.Post, "/api/v1/quote-comparisons", new { clientId = Guid.NewGuid(), title = "T", requestedOn = "2026-11-10" }), HttpStatusCode.NotFound) ||
            !await IsError(await client.GetAsync("/api/v1/quote-comparisons?status=cerrada"), HttpStatusCode.BadRequest, "Status must be one of: abierta, decidida") ||
            !await IsError(await client.GetAsync($"/api/v1/quote-comparisons/{Guid.NewGuid()}"), HttpStatusCode.NotFound) ||
            await ProviderState() != providersBefore)
            throw new Exception("Rejected comparisons changed stored records");
        var comparisonResponse = await Send(HttpMethod.Post, "/api/v1/quote-comparisons", new { clientId = id, title = "Herbicidas", requestedOn = "2026-11-10" });
        var comparisonRow = await Body(comparisonResponse);
        var comparisonId = comparisonRow.GetProperty("id").GetGuid();
        if (comparisonResponse.StatusCode != HttpStatusCode.Created || comparisonRow.GetProperty("status").GetString() != "abierta" ||
            !Absent(comparisonRow, "chosenQuoteId", "decisionReason") || comparisonRow.GetProperty("requestedOn").GetString() != "2026-11-10")
            throw new Exception($"Comparison create: {(int)comparisonResponse.StatusCode}");
        var otherComparisonId = (await Body(await Send(HttpMethod.Post, "/api/v1/quote-comparisons", new { clientId = richId, title = "Fertilizantes", requestedOn = "2026-11-05" })))
            .GetProperty("id").GetGuid();
        var quoteBody = new { providerId, receivedOn = (string?)"2026-11-12", currency = "ARS", notes = (string?)null,
            lines = new object[] { new { inputId, quantity = 10m, unitPrice = 5.5m }, new { productText = "Coadyuvante X", quantity = 2m, unit = "l", unitPrice = 3.3333m } } };
        providersBefore = await ProviderState();
        if (!await IsError(await Send(HttpMethod.Post, $"/api/v1/quote-comparisons/{comparisonId}/quotes", quoteBody with { lines = new object[] { new { quantity = 1m, unit = "l", unitPrice = 1m } } }),
                HttpStatusCode.BadRequest, "Each line needs an input or a product") ||
            !await IsError(await Send(HttpMethod.Post, $"/api/v1/quote-comparisons/{comparisonId}/quotes", quoteBody with { lines = new object[] { new { inputId, quantity = 0m, unitPrice = 1m } } }),
                HttpStatusCode.BadRequest, "Quantity must be positive") ||
            !await IsError(await Send(HttpMethod.Post, $"/api/v1/quote-comparisons/{comparisonId}/quotes", quoteBody with { lines = new object[] { new { inputId, quantity = 1m, unitPrice = -1m } } }),
                HttpStatusCode.BadRequest, "Unit price must be zero or positive") ||
            !await IsError(await Send(HttpMethod.Post, $"/api/v1/quote-comparisons/{comparisonId}/quotes", quoteBody with { currency = "EUR" }), HttpStatusCode.BadRequest, "Currency must be ARS or USD") ||
            !await IsError(await Send(HttpMethod.Post, $"/api/v1/quote-comparisons/{Guid.NewGuid()}/quotes", quoteBody), HttpStatusCode.NotFound, "Record not found") ||
            await ProviderState() != providersBefore)
            throw new Exception("Rejected quotes changed stored records");
        var quoteResponse = await Send(HttpMethod.Post, $"/api/v1/quote-comparisons/{comparisonId}/quotes", quoteBody);
        var quoteRow = await Body(quoteResponse);
        var quoteId = quoteRow.GetProperty("id").GetGuid();
        var quoteLines = quoteRow.GetProperty("lines");
        if (quoteResponse.StatusCode != HttpStatusCode.Created || quoteRow.GetProperty("comparisonId").GetGuid() != comparisonId || quoteRow.GetProperty("total").GetDecimal() != 61.6666m ||
            quoteLines.GetArrayLength() != 2 || quoteLines[0].GetProperty("position").GetInt32() != 1 || quoteLines[0].GetProperty("unit").GetString() != "kg" ||
            quoteLines[0].GetProperty("inputId").GetGuid() != inputId || !Absent(quoteLines[0], "productText") || !Absent(quoteLines[1], "inputId") ||
            quoteLines[1].GetProperty("productText").GetString() != "Coadyuvante X" || quoteRow.GetProperty("receivedOn").GetString() != "2026-11-12")
            throw new Exception($"Quote create: {(int)quoteResponse.StatusCode} {quoteRow}");
        var cheapQuoteId = (await Body(await Send(HttpMethod.Post, $"/api/v1/quote-comparisons/{comparisonId}/quotes", quoteBody with { providerId = labId, receivedOn = "2026-11-11",
            lines = new object[] { new { inputId, quantity = 10m, unitPrice = 5m } } }))).GetProperty("id").GetGuid();
        var usdQuoteId = (await Body(await Send(HttpMethod.Post, $"/api/v1/quote-comparisons/{comparisonId}/quotes", quoteBody with { currency = "USD", receivedOn = "2026-11-13",
            lines = new object[] { new { inputId, quantity = 10m, unitPrice = 0.005m } } }))).GetProperty("id").GetGuid();
        var foreignQuoteId = (await Body(await Send(HttpMethod.Post, $"/api/v1/quote-comparisons/{otherComparisonId}/quotes", quoteBody))).GetProperty("id").GetGuid();
        var detailRow = await client.GetFromJsonAsync<JsonElement>($"/api/v1/quote-comparisons/{comparisonId}");
        var detailProducts = detailRow.GetProperty("products");
        var inputProduct = detailProducts[1];
        var cheapest = inputProduct.GetProperty("cheapestQuoteIdByCurrency");
        var firstOffer = inputProduct.GetProperty("offers")[0];
        if (detailRow.GetProperty("comparison").GetProperty("id").GetGuid() != comparisonId ||
            !detailRow.GetProperty("quotes").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).SequenceEqual([cheapQuoteId, quoteId, usdQuoteId]) ||
            detailProducts.GetArrayLength() != 2 || detailProducts[0].GetProperty("key").GetString() != "text:coadyuvante x" ||
            detailProducts[0].GetProperty("label").GetString() != "Coadyuvante X" || inputProduct.GetProperty("key").GetString() != $"input:{inputId}" ||
            inputProduct.GetProperty("label").GetString() != "Producto de prueba" || inputProduct.GetProperty("offers").GetArrayLength() != 3 ||
            firstOffer.GetProperty("quoteId").GetGuid() != cheapQuoteId || firstOffer.GetProperty("providerId").GetGuid() != labId ||
            firstOffer.GetProperty("unitPrice").GetDecimal() != 5m || firstOffer.GetProperty("currency").GetString() != "ARS" || firstOffer.GetProperty("quantity").GetDecimal() != 10m ||
            firstOffer.GetProperty("unit").GetString() != "kg" || firstOffer.GetProperty("lineTotal").GetDecimal() != 50m ||
            cheapest.EnumerateObject().Count() != 2 || cheapest.GetProperty("ARS").GetGuid() != cheapQuoteId || cheapest.GetProperty("USD").GetGuid() != usdQuoteId ||
            detailProducts[0].GetProperty("cheapestQuoteIdByCurrency").EnumerateObject().Single() is not { Name: "ARS" } textCheapest || textCheapest.Value.GetGuid() != quoteId)
            throw new Exception($"Comparison detail shape: {detailRow}");
        providersBefore = await ProviderState();
        if (!await IsError(await Send(HttpMethod.Post, $"/api/v1/quote-comparisons/{comparisonId}/decide", new { quoteId = cheapQuoteId, reason = " ", revision = 1 }), HttpStatusCode.BadRequest,
                "Reason is required") ||
            !await IsError(await Send(HttpMethod.Post, $"/api/v1/quote-comparisons/{comparisonId}/decide", new { quoteId = foreignQuoteId, reason = "Motivo", revision = 1 }), HttpStatusCode.NotFound) ||
            !await IsError(await Send(HttpMethod.Post, $"/api/v1/quote-comparisons/{comparisonId}/decide", new { quoteId = cheapQuoteId, reason = "Motivo", revision = 0 }), HttpStatusCode.Conflict, "Stale revision") ||
            !await IsError(await Send(HttpMethod.Post, $"/api/v1/quote-comparisons/{comparisonId}/reopen", new { revision = 1 }), HttpStatusCode.Conflict, "Comparison is not decided") ||
            await ProviderState() != providersBefore)
            throw new Exception("Rejected decisions changed stored records");
        var decidedResponse = await Send(HttpMethod.Post, $"/api/v1/quote-comparisons/{comparisonId}/decide", new { quoteId = cheapQuoteId, reason = "Menor precio", revision = 1 });
        if (decidedResponse.StatusCode != HttpStatusCode.OK || (await Body(decidedResponse)) is var decidedRow && (decidedRow.GetProperty("status").GetString() != "decidida" ||
            decidedRow.GetProperty("chosenQuoteId").GetGuid() != cheapQuoteId || decidedRow.GetProperty("decisionReason").GetString() != "Menor precio" ||
            decidedRow.GetProperty("revision").GetInt64() != 2))
            throw new Exception("Decision failed");
        providersBefore = await ProviderState();
        if (!await IsError(await Send(HttpMethod.Put, $"/api/v1/quote-comparisons/{comparisonId}", new { title = "Nuevo", requestedOn = "2026-11-10", revision = 2 }), HttpStatusCode.Conflict, "Comparison is not open") ||
            !await IsError(await Send(HttpMethod.Post, $"/api/v1/quote-comparisons/{comparisonId}/quotes", quoteBody), HttpStatusCode.Conflict, "Comparison is not open") ||
            !await IsError(await Send(HttpMethod.Put, $"/api/v1/quotes/{quoteId}", new { providerId, receivedOn = "2026-11-12", currency = "ARS", lines = quoteBody.lines, revision = 1 }), HttpStatusCode.Conflict, "Comparison is not open") ||
            await ProviderState() != providersBefore)
            throw new Exception("Edits of a decided comparison changed stored records");
        var reopenedResponse = await Send(HttpMethod.Post, $"/api/v1/quote-comparisons/{comparisonId}/reopen", new { revision = 2 });
        if (reopenedResponse.StatusCode != HttpStatusCode.OK || (await Body(reopenedResponse)) is var reopenedRow && (reopenedRow.GetProperty("status").GetString() != "abierta" ||
            reopenedRow.GetProperty("decisionReason").GetString() != "Menor precio" || reopenedRow.GetProperty("chosenQuoteId").GetGuid() != cheapQuoteId))
            throw new Exception("Reopen failed or dropped the previous decision");
        var editedQuote = await Send(HttpMethod.Put, $"/api/v1/quotes/{quoteId}", new { receivedOn = "2026-11-12", currency = "USD", notes = "Nota",
            lines = new object[] { new { productText = "Producto libre", quantity = 3m, unit = "kg", unitPrice = 2m } }, revision = 1 });
        if (editedQuote.StatusCode != HttpStatusCode.OK || (await Body(editedQuote)) is var editedQuoteRow && (editedQuoteRow.GetProperty("providerId").GetGuid() != providerId ||
            editedQuoteRow.GetProperty("total").GetDecimal() != 6m || editedQuoteRow.GetProperty("lines").GetArrayLength() != 1 ||
            editedQuoteRow.GetProperty("currency").GetString() != "USD" || editedQuoteRow.GetProperty("revision").GetInt64() != 2))
            throw new Exception("Quote edit after reopening failed");
        if (!await IsError(await Send(HttpMethod.Put, $"/api/v1/quotes/{quoteId}", new { receivedOn = "2026-11-12", currency = "USD", lines = new object[0], revision = 1 }), HttpStatusCode.Conflict, "Stale revision") ||
            !await IsError(await Send(HttpMethod.Put, $"/api/v1/quotes/{Guid.NewGuid()}", new { receivedOn = "2026-11-12", currency = "USD", lines = new object[0], revision = 1 }), HttpStatusCode.NotFound))
            throw new Exception("Rejected quote edit accepted");
        var renamedComparison = await Send(HttpMethod.Put, $"/api/v1/quote-comparisons/{comparisonId}", new { title = "Herbicidas de barbecho", requestedOn = "2026-11-09", revision = 3 });
        if (renamedComparison.StatusCode != HttpStatusCode.OK || (await Body(renamedComparison)).GetProperty("title").GetString() != "Herbicidas de barbecho" ||
            !await IsError(await Send(HttpMethod.Put, $"/api/v1/quote-comparisons/{comparisonId}", new { title = "Stale", requestedOn = "2026-11-09", revision = 3 }), HttpStatusCode.Conflict, "Stale revision"))
            throw new Exception("Comparison edit after reopening failed");
        if (!(await Ids($"quote-comparisons?clientId={id}")).SequenceEqual([comparisonId]) || !(await Ids("quote-comparisons")).SequenceEqual([comparisonId, otherComparisonId]) ||
            !(await Ids("quote-comparisons?status=abierta")).SequenceEqual([comparisonId, otherComparisonId]) || (await Ids("quote-comparisons?status=decidida")).Length != 0 ||
            !(await Ids($"quote-comparisons?clientId={richId}&status=abierta")).SequenceEqual([otherComparisonId]))
            throw new Exception("Comparisons filter mismatch");

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
