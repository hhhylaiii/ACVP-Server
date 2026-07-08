var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.Run();

/// <summary>Exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program;
