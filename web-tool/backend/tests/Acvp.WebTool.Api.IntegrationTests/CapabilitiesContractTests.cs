using System.Net;
using System.Net.Http.Json;
using Acvp.WebTool.Api.IntegrationTests.Infrastructure;
using Acvp.WebTool.Api.Models;
using FluentAssertions;
using Xunit;

namespace Acvp.WebTool.Api.IntegrationTests;

/// <summary>T017 — GET /api/capabilities returns the frozen ML-KEM/ML-DSA matrix.</summary>
public sealed class CapabilitiesContractTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;

    public CapabilitiesContractTests(TestWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetCapabilities_ReturnsSupportedMatrix()
    {
        var response = await _client.GetAsync("/api/capabilities");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var capabilities = await response.Content.ReadFromJsonAsync<Capabilities>();

        capabilities!.Algorithms.Should().HaveCount(2);

        var mlKem = capabilities.Algorithms.Single(a => a.Algorithm == "ML-KEM");
        mlKem.Modes.Should().BeEquivalentTo("keyGen", "encapDecap");
        mlKem.ParameterSets.Should().BeEquivalentTo("ML-KEM-512", "ML-KEM-768", "ML-KEM-1024");

        var mlDsa = capabilities.Algorithms.Single(a => a.Algorithm == "ML-DSA");
        mlDsa.Modes.Should().BeEquivalentTo("keyGen", "sigGen", "sigVer");
        mlDsa.ParameterSets.Should().BeEquivalentTo("ML-DSA-44", "ML-DSA-65", "ML-DSA-87");
    }
}
