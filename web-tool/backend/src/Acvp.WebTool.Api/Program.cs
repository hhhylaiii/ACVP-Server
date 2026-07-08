using Acvp.WebTool.Api.Endpoints;
using Acvp.WebTool.Api.Options;
using Acvp.WebTool.Api.Services;
using Acvp.WebTool.Api.Validation;
using NIST.CVP.ACVTS.Libraries.Common.Config;
using NIST.CVP.ACVTS.Libraries.Common.Interfaces;
using NIST.CVP.ACVTS.Libraries.Common.Services;
using NIST.CVP.ACVTS.Libraries.Crypto.Oracle;
using NIST.CVP.ACVTS.Libraries.Generation;
using NIST.CVP.ACVTS.Libraries.Generation.Core;
using NIST.CVP.ACVTS.Libraries.Math;
using NIST.CVP.ACVTS.Libraries.Oracle.Abstractions;

var builder = WebApplication.CreateBuilder(args);

// --- gen-val engine configuration (mirrors EntryPointConfigHelper; upstream code unmodified) ---
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IDbConnectionStringFactory, DbConnectionStringFactory>();
builder.Services.AddSingleton<IDbConnectionFactory, SqlDbConnectionFactory>();
builder.Services.Configure<EnvironmentConfig>(builder.Configuration.GetSection(nameof(EnvironmentConfig)));
builder.Services.Configure<PoolConfig>(builder.Configuration.GetSection(nameof(PoolConfig)));
builder.Services.Configure<OrleansConfig>(builder.Configuration.GetSection(nameof(OrleansConfig)));

// --- Orleans-backed crypto oracle (client side only; the silo itself runs separately) ---
builder.Services.AddSingleton<IOrleansClientClustering, LocalOrleansClientClustering>();
builder.Services.AddSingleton<IClusterClientFactory, ClusterClientFactory>();
builder.Services.AddSingleton<IRandom800_90, Random800_90>();
builder.Services.AddSingleton<IOracle, Oracle>();
builder.Services.AddSingleton<IGenValInvoker>(sp => new GenValInvoker(sp));

// --- web tool services ---
builder.Services.Configure<EngineOptions>(builder.Configuration.GetSection(EngineOptions.SectionName));
builder.Services.Configure<LimitsOptions>(builder.Configuration.GetSection(LimitsOptions.SectionName));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.AddSingleton<IArtifactStore, ArtifactStore>();
builder.Services.AddSingleton<IJobStore, InMemoryJobStore>();
builder.Services.AddSingleton<JobQueue>();
builder.Services.AddSingleton<IJobQueue>(sp => sp.GetRequiredService<JobQueue>());
builder.Services.AddSingleton<IGenValService, GenValService>();
builder.Services.AddHostedService<JobWorkerService>();
builder.Services.AddSingleton<ConfigurationValidator>();
builder.Services.AddSingleton<RegistrationBuilder>();
builder.Services.AddSingleton<PromptPackageBuilder>();
builder.Services.AddSingleton<GenerateJobHandler>();
builder.Services.AddSingleton<ResponseUploadValidator>();
builder.Services.AddSingleton<ValidationReportBuilder>();
builder.Services.AddSingleton<ValidateJobHandler>();

var app = builder.Build();

app.UseMiddleware<SafeErrorMiddleware>();

// Serve the built SPA when bundled (production); during development the Vite
// dev server proxies /api to this host instead.
app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");
api.MapGet("/health", () => Results.Ok(new { status = "ok" }));
api.MapCapabilitiesEndpoints();
api.MapCheckEndpoints();
api.MapGenerateEndpoints();
api.MapJobsEndpoints();
api.MapValidateEndpoints();
api.MapReportEndpoints();

var indexHtml = Path.Combine(app.Environment.WebRootPath ?? string.Empty, "index.html");
if (File.Exists(indexHtml))
{
    app.MapFallbackToFile("index.html");
}

app.Run();

/// <summary>Exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program;
