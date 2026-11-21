var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Only operational health is public. No domain routes exist until authentication is implemented.
app.MapGet("/api/v1/health", () => Results.Json(new { status = "healthy" }));

app.Run();
