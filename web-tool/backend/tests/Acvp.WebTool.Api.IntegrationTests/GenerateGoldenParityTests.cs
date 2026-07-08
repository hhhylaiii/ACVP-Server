using System.IO.Compression;
using System.Net.Http.Json;
using Acvp.WebTool.Api.IntegrationTests.Infrastructure;
using Acvp.WebTool.Api.Models;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Acvp.WebTool.Api.IntegrationTests;

/// <summary>
/// T023 — the tool's generation drives the same engine as the GenValAppRunner CLI,
/// so the produced prompt must have the CLI's structure for the same registration:
/// correct algorithm/mode/revision, the requested parameter sets, and non-empty
/// per-case questions each carrying a tcId.
/// </summary>
public sealed class GenerateGoldenParityTests : IClassFixture<RealEngineWebAppFactory>
{
    private readonly RealEngineWebAppFactory _factory;

    public GenerateGoldenParityTests(RealEngineWebAppFactory factory)
    {
        _factory = factory;
    }

    [SiloFact]
    public async Task Generate_MlKemKeyGen_ProducesEngineParityPrompt()
    {
        var client = _factory.CreateClient();

        var accepted = await client.PostAsJsonAsync("/api/generate", new AlgorithmConfiguration(
            "ML-KEM", "keyGen", ["ML-KEM-512", "ML-KEM-768", "ML-KEM-1024"]));
        var job = (await accepted.Content.ReadFromJsonAsync<Job>())!;
        var final = await JobPolling.WaitForTerminalAsync(client, job.JobId, TimeSpan.FromMinutes(5));

        final.Status.Should().Be(JobStatus.Succeeded, $"generation failed: {final.Error?.Message}");

        var package = await client.GetAsync($"/api/jobs/{job.JobId}/prompt-package");
        await using var stream = await package.Content.ReadAsStreamAsync();
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        using var reader = new StreamReader(zip.Entries.Single(e => e.Name == "prompt.json").Open());
        var prompt = JObject.Parse(await reader.ReadToEndAsync());

        prompt.Value<string>("algorithm").Should().Be("ML-KEM");
        prompt.Value<string>("mode").Should().Be("keyGen");
        prompt.Value<string>("revision").Should().Be("FIPS203");

        var groups = (JArray)prompt["testGroups"]!;
        groups.Should().NotBeEmpty();
        groups.Select(g => g.Value<string>("parameterSet")).Distinct()
            .Should().BeEquivalentTo("ML-KEM-512", "ML-KEM-768", "ML-KEM-1024");

        var tests = groups.SelectMany(g => g["tests"]!).ToArray();
        tests.Should().NotBeEmpty();
        tests.Select(t => t.Value<int?>("tcId")).Should().NotContainNulls()
            .And.OnlyHaveUniqueItems();
    }
}
