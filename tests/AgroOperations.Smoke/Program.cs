using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;

using var listener = new TcpListener(IPAddress.Loopback, 0);
listener.Start();
var port = ((IPEndPoint)listener.LocalEndpoint).Port;
listener.Stop();

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
var api = Path.GetFullPath(Path.Combine(root, "../src/AgroOperations.Api/AgroOperations.Api.csproj"));
var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
var start = new ProcessStartInfo("dotnet")
{
    ArgumentList = { "run", "--project", api, "--no-launch-profile", "--", "--urls", $"http://127.0.0.1:{port}" },
    UseShellExecute = false,
    RedirectStandardOutput = true,
    RedirectStandardError = true
};
start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
start.Environment["AGRO_LOCAL_ADMIN_PASSWORD"] = password;
start.Environment["AGRO_LOCAL_ADMIN_EMAIL"] = "owner@example.test";
using var server = Process.Start(start) ?? throw new Exception("Unable to start API");
try
{
    using var handler = new HttpClientHandler { UseCookies = true };
    using var client = new HttpClient(handler) { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(10) };
    HttpResponseMessage? health = null;
    for (var attempt = 0; attempt < 40; attempt++)
    {
        if (server.HasExited) throw new Exception($"API exited with code {server.ExitCode}");
        try { health = await client.GetAsync("/api/v1/health"); break; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { await Task.Delay(250); }
    }
    if (health is null) throw new Exception("API did not start");
    var body = await health.Content.ReadAsStringAsync();
    if (health.StatusCode != HttpStatusCode.OK || body != "{\"status\":\"healthy\"}")
        throw new Exception($"Health: {(int)health.StatusCode} {body}");
    var domain = await client.PostAsync("/api/v1/records", new StringContent("{}"));
    if (domain.StatusCode != HttpStatusCode.NotFound)
        throw new Exception($"Unexpected anonymous write status: {(int)domain.StatusCode}");
    var wrongVersion = await client.GetAsync("/api/v2/health");
    if (wrongVersion.StatusCode != HttpStatusCode.NotFound)
        throw new Exception($"Unexpected v2 health status: {(int)wrongVersion.StatusCode}");
    var domainRead = await client.GetAsync("/api/v1/records");
    if (domainRead.StatusCode != HttpStatusCode.NotFound)
        throw new Exception($"Unexpected anonymous domain read status: {(int)domainRead.StatusCode}");
    var anonymousSession = await client.GetAsync("/api/v1/session");
    if (anonymousSession.StatusCode != HttpStatusCode.Unauthorized) throw new Exception("Session was accessible anonymously");
    var incorrect = await client.PostAsJsonAsync("/api/v1/session", new { password = "incorrect" });
    if (incorrect.StatusCode != HttpStatusCode.Unauthorized) throw new Exception("Invalid password accepted");
    if (handler.CookieContainer.GetCookies(client.BaseAddress!).Count != 0) throw new Exception("Rejected login set a cookie");
    var login = await client.PostAsJsonAsync("/api/v1/session", new { password });
    if (login.StatusCode != HttpStatusCode.OK) throw new Exception($"Local login: {(int)login.StatusCode}");
    var session = await client.GetAsync("/api/v1/session");
    if (session.StatusCode != HttpStatusCode.OK) throw new Exception("Authenticated session unavailable");
    using var parsed = JsonDocument.Parse(await session.Content.ReadAsStringAsync());
    var csrf = parsed.RootElement.GetProperty("csrfToken").GetString();
    if (string.IsNullOrEmpty(csrf)) throw new Exception("Missing session CSRF token");
    var noCsrf = await client.PostAsync("/api/v1/session/logout", null);
    if (noCsrf.StatusCode != HttpStatusCode.Forbidden) throw new Exception("Logout accepted without CSRF");
    using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/session/logout");
    request.Headers.Add("X-Agro-CSRF", csrf);
    var logout = await client.SendAsync(request);
    if (logout.StatusCode != HttpStatusCode.NoContent) throw new Exception("Logout failed");
    if ((await client.GetAsync("/api/v1/session")).StatusCode != HttpStatusCode.Unauthorized)
        throw new Exception("Session remained active after logout");
    Console.WriteLine("PASS: health, anonymous domain absence, local login, CSRF logout and session invalidation");
}
finally
{
    if (!server.HasExited) server.Kill(entireProcessTree: true);
    await server.WaitForExitAsync();
}

start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
using var production = Process.Start(start) ?? throw new Exception("Unable to start production API");
try
{
    using var check = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(10) };
    HttpResponseMessage? ready = null;
    for (var attempt = 0; attempt < 40; attempt++)
    {
        if (production.HasExited) throw new Exception($"Production API exited with code {production.ExitCode}");
        try { ready = await check.GetAsync("/api/v1/health"); break; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { await Task.Delay(250); }
    }
    if (ready?.StatusCode != HttpStatusCode.OK ||
        (await check.GetAsync("/api/v1/session")).StatusCode != HttpStatusCode.NotFound)
        throw new Exception("Production exposed temporary local login");
    Console.WriteLine("PASS: local bootstrap not mapped in production");
}
finally
{
    if (!production.HasExited) production.Kill(entireProcessTree: true);
    await production.WaitForExitAsync();
}
