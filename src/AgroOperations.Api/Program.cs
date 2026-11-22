using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var localPassword = Environment.GetEnvironmentVariable("AGRO_LOCAL_ADMIN_PASSWORD");
var localEmail = Environment.GetEnvironmentVariable("AGRO_LOCAL_ADMIN_EMAIL");
var localAccess = builder.Environment.IsDevelopment() &&
    !string.IsNullOrWhiteSpace(localEmail) && !string.IsNullOrWhiteSpace(localPassword) &&
    localPassword.Length >= 24;

if (localAccess)
{
    // Persistent local keys keep sessions working across restarts; this directory is Git-ignored.
    var keyDirectory = Path.Combine(builder.Environment.ContentRootPath, ".local", "keys");
    Directory.CreateDirectory(keyDirectory);
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
    {
        options.Cookie.Name = "agro_local_session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.Path = "/api/v1";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = false;
        options.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
        options.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
    });
    builder.Services.AddAuthorization();
    builder.Services.AddRateLimiter(options => options.AddPolicy("local-login", context =>
        RateLimitPartition.GetFixedWindowLimiter("local-login", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        })));
}

var app = builder.Build();
app.MapGet("/api/v1/health", () => Results.Json(new { status = "healthy" }));

if (localAccess)
{
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    var local = app.MapGroup("/api/v1").AddEndpointFilter(async (context, next) =>
    {
        var address = context.HttpContext.Connection.RemoteIpAddress;
        return address is null || !IPAddress.IsLoopback(address)
            ? Results.NotFound() : await next(context);
    });
    local.MapPost("/session", async (HttpContext context, LoginRequest request) =>
    {
        var supplied = SHA256.HashData(Encoding.UTF8.GetBytes(request.Password ?? ""));
        var expected = SHA256.HashData(Encoding.UTF8.GetBytes(localPassword!));
        if (!CryptographicOperations.FixedTimeEquals(supplied, expected)) return Results.Unauthorized();
        var csrf = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, localEmail!), new Claim("csrf", csrf)],
            CookieAuthenticationDefaults.AuthenticationScheme));
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        return Results.Json(new { email = localEmail, csrfToken = csrf });
    }).RequireRateLimiting("local-login");
    local.MapGet("/session", (ClaimsPrincipal principal) => Results.Json(new
    {
        email = principal.FindFirstValue(ClaimTypes.NameIdentifier),
        csrfToken = principal.FindFirstValue("csrf")
    })).RequireAuthorization();
    local.MapPost("/session/logout", async (HttpContext context, ClaimsPrincipal principal) =>
    {
        var token = context.Request.Headers["X-Agro-CSRF"].ToString();
        if (string.IsNullOrEmpty(token) || token != principal.FindFirstValue("csrf"))
            return Results.StatusCode(403);
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.NoContent();
    }).RequireAuthorization();
}

app.Run();

internal sealed record LoginRequest(string? Password);
