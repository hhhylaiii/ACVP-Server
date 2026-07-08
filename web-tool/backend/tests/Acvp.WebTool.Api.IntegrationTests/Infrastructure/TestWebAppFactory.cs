using Acvp.WebTool.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Acvp.WebTool.Api.IntegrationTests.Infrastructure;

/// <summary>
/// WebApplicationFactory that swaps the Orleans-backed engine for
/// <see cref="FakeGenValService"/> and stores artifacts under a temp directory.
/// </summary>
public class TestWebAppFactory : WebApplicationFactory<Program>
{
    public FakeGenValService FakeEngine { get; } = new();

    public string ArtifactRoot { get; } =
        Path.Combine(Path.GetTempPath(), $"acvp-webtool-it-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WebTool:Storage:ArtifactRoot"] = ArtifactRoot,
                ["WebTool:Limits:MaxUploadBytes"] = "1048576",
                ["WebTool:Limits:MaxConcurrentJobs"] = "2",
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(IGenValService));
            services.AddSingleton<IGenValService>(FakeEngine);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (Directory.Exists(ArtifactRoot))
        {
            Directory.Delete(ArtifactRoot, recursive: true);
        }
    }
}
