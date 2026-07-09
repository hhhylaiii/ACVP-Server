using System.Net;
using System.Net.Http.Json;
using Acvp.WebTool.Api.IntegrationTests.Infrastructure;
using Acvp.WebTool.Api.Models;
using FluentAssertions;
using Xunit;

namespace Acvp.WebTool.Api.IntegrationTests;

/// <summary>T018 — POST /api/check validates a configuration synchronously.</summary>
public sealed class CheckContractTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;

    public CheckContractTests(TestWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Check_ValidConfiguration_Returns200Valid()
    {
        var response = await _client.PostAsJsonAsync("/api/check", new AlgorithmConfiguration(
            "ML-KEM", "keyGen", ["ML-KEM-768"]));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<CheckResult>();
        result!.Valid.Should().BeTrue();
    }

    [Theory]
    [InlineData("ML-KEM", "sigGen", "ML-KEM-768")]
    [InlineData("ML-DSA", "encapDecap", "ML-DSA-65")]
    [InlineData("ML-KEM", "keyGen", "ML-DSA-65")]
    [InlineData("RSA", "keyGen", "ML-KEM-768")]
    public async Task Check_UnsupportedCombination_Returns400UnsupportedSelection(
        string algorithm, string mode, string parameterSet)
    {
        var response = await _client.PostAsJsonAsync("/api/check", new AlgorithmConfiguration(
            algorithm, mode, [parameterSet]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<SafeError>();
        error!.Code.Should().Be(SafeErrorCodes.UnsupportedSelection);
        error.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Check_EmptyParameterSets_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/check", new AlgorithmConfiguration(
            "ML-KEM", "keyGen", []));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<SafeError>();
        error!.Code.Should().BeOneOf(SafeErrorCodes.InvalidConfiguration, SafeErrorCodes.UnsupportedSelection);
    }
}
