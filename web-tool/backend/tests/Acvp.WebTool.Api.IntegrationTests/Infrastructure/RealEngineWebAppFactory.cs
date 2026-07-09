using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Acvp.WebTool.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Factory that keeps the REAL Orleans-backed engine (no fake) for golden-parity
/// tests. Requires a running silo; guarded by <see cref="SiloFactAttribute"/>.
/// </summary>
public sealed class RealEngineWebAppFactory : WebApplicationFactory<Program>
{
    public string ArtifactRoot { get; } =
        Path.Combine(Path.GetTempPath(), $"acvp-webtool-golden-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WebTool:Storage:ArtifactRoot"] = ArtifactRoot,
            });
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
