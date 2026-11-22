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
        foreach (var name in new[] { "001_operations.sql", "003_crops.sql" })
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
        foreach (var route in new[] { "clients", "fields", "plots", "crops" })
            if ((await client.GetAsync($"/api/v1/{route}")).StatusCode != HttpStatusCode.Unauthorized ||
                (await client.PostAsJsonAsync($"/api/v1/{route}", new { name = "Example" })).StatusCode != HttpStatusCode.Unauthorized)
                throw new Exception($"Anonymous {route} route accessible");
        var login = await client.PostAsJsonAsync("/api/v1/session", new { password });
        if (login.StatusCode != HttpStatusCode.OK) throw new Exception("Local session unavailable");
        using var session = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var csrf = session.RootElement.GetProperty("csrfToken").GetString()!;
        if ((await client.PostAsJsonAsync("/api/v1/clients", new { name = "Example" })).StatusCode != HttpStatusCode.Forbidden)
            throw new Exception("Business write accepted without CSRF");
        async Task<HttpResponseMessage> Send(HttpMethod method, string path, object value)
        {
            using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(value) };
            request.Headers.Add("X-Agro-CSRF", csrf);
            return await client.SendAsync(request);
        }
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
        if ((await Send(HttpMethod.Post, "/api/v1/crops", new { name = " CROP " })).StatusCode != HttpStatusCode.BadRequest ||
            (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/crops"))!.Length != 1)
            throw new Exception("Duplicate crop changed stored records");
        if ((await client.GetFromJsonAsync<JsonElement[]>("/api/v1/fields"))!.Length != 1 ||
            (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/plots"))!.Length != 1 ||
            (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/crops"))!.Length != 1)
            throw new Exception("Master listings incomplete");
        if ((await Send(HttpMethod.Put, $"/api/v1/clients/{id}", new { name = "Stale", revision = 0 })).StatusCode != HttpStatusCode.Conflict ||
            (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/clients"))![0].GetProperty("name").GetString() != "Sample")
            throw new Exception("Stale client update overwrote record");
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
