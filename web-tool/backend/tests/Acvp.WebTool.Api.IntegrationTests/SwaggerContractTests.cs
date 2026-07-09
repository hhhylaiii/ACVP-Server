using System.Net;
using Acvp.WebTool.Api.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Acvp.WebTool.Api.IntegrationTests;

/// <summary>Swagger UI is exposed in Development only; production serves the SPA without it.</summary>
public sealed class SwaggerContractTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;

    public SwaggerContractTests(TestWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SwaggerJson_InDevelopment_DescribesTheApi()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var document = await response.Content.ReadAsStringAsync();
        document.Should().Contain("FIPS 203/204 Validation Web Tool API");
        document.Should().Contain("/api/capabilities");
        document.Should().Contain("/api/generate");
        document.Should().Contain("/api/validate");
        document.Should().Contain("/api/jobs/{jobId}");
    }

    [Fact]
    public async Task SwaggerUi_InDevelopment_IsServed()
    {
        var response = await _client.GetAsync("/swagger/index.html");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Swagger_InProduction_IsNotExposed()
    {
        using var factory = new ProductionWebAppFactory();
        using var client = factory.CreateClient();

        (await client.GetAsync("/swagger/v1/swagger.json")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync("/swagger/index.html")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Swagger_InProduction_CanBeEnabledByConfiguration()
    {
        using var factory = new ProductionWithSwaggerWebAppFactory();
        using var client = factory.CreateClient();

        (await client.GetAsync("/swagger/v1/swagger.json")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/swagger/index.html")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private class ProductionWebAppFactory : TestWebAppFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseEnvironment("Production");
        }
    }

    private sealed class ProductionWithSwaggerWebAppFactory : ProductionWebAppFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["WebTool:Swagger:Enabled"] = "true",
                });
            });
        }
    }
}
