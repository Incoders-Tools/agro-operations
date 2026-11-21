using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

using var listener = new TcpListener(IPAddress.Loopback, 0);
listener.Start();
var port = ((IPEndPoint)listener.LocalEndpoint).Port;
listener.Stop();

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
var api = Path.GetFullPath(Path.Combine(root, "../src/AgroOperations.Api/AgroOperations.Api.csproj"));
using var server = Process.Start(new ProcessStartInfo("dotnet")
{
    ArgumentList = { "run", "--project", api, "--no-launch-profile", "--", "--urls", $"http://127.0.0.1:{port}" },
    UseShellExecute = false,
    RedirectStandardOutput = true,
    RedirectStandardError = true
}) ?? throw new Exception("Unable to start API");
try
{
    using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(10) };
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
    Console.WriteLine("PASS: public versioned health and no anonymous domain routes");
}
finally
{
    if (!server.HasExited) server.Kill(entireProcessTree: true);
    await server.WaitForExitAsync();
}
