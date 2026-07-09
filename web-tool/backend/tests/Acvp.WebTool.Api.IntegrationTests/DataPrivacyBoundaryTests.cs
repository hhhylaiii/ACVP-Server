using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using Acvp.WebTool.Api.IntegrationTests.Infrastructure;
using Acvp.WebTool.Api.Models;
using FluentAssertions;
using Xunit;

namespace Acvp.WebTool.Api.IntegrationTests;

/// <summary>
/// T021 — FR-011 / SC-007: internalProjection.json and expectedResults.json are
/// never exposed by the prompt package or any API route; only prompt/response
/// artifacts are exchanged.
/// </summary>
public sealed class DataPrivacyBoundaryTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;
    private readonly HttpClient _client;

    public DataPrivacyBoundaryTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PromptPackage_NeverContainsServerOnlyArtifacts()
    {
        var jobId = await GenerateSucceededJobAsync();

        var response = await _client.GetAsync($"/api/jobs/{jobId}/prompt-package");
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

        zip.Entries.Select(e => e.Name).Should().NotContain(
            ["internalProjection.json", "expectedResults.json"],
            "the answer key must stay server-side (FR-011)");
    }

    [Fact]
    public async Task NoApiRoute_ServesServerOnlyArtifacts()
    {
        var jobId = await GenerateSucceededJobAsync();

        foreach (var path in new[]
                 {
                     $"/api/jobs/{jobId}/internalProjection.json",
                     $"/api/jobs/{jobId}/expectedResults.json",
                     $"/api/jobs/{jobId}/artifacts/internalProjection.json",
                     $"/api/jobs/{jobId}/artifacts/expectedResults.json",
                     $"/artifacts/{jobId}/internalProjection.json",
                     $"/artifacts/{jobId}/expectedResults.json",
                 })
        {
            var response = await _client.GetAsync(path);
            response.StatusCode.Should().Be(HttpStatusCode.NotFound, $"{path} must not exist");
        }
    }

    [Fact]
    public async Task ServerOnlyArtifacts_ExistOnDiskButOnlyServerSide()
    {
        var jobId = await GenerateSucceededJobAsync();

        // Retained server-side for later validation…
        File.Exists(Path.Combine(_factory.ArtifactRoot, jobId, "internalProjection.json")).Should().BeTrue();
        File.Exists(Path.Combine(_factory.ArtifactRoot, jobId, "expectedResults.json")).Should().BeTrue();

        // …and the tool never stores anything except the known prompt/response artifacts
        // (no IUT source code or private key material is ever requested, SC-007).
        var files = Directory.GetFiles(Path.Combine(_factory.ArtifactRoot, jobId)).Select(Path.GetFileName);
        files.Should().BeSubsetOf(new[]
        {
            "prompt.json",
            "internalProjection.json",
            "expectedResults.json",
            "exampleResponses.json",
            "instructions.md",
            "responses.json",
            "validation.json",
        });
    }

    private async Task<string> GenerateSucceededJobAsync()
    {
        var accepted = await _client.PostAsJsonAsync("/api/generate", new AlgorithmConfiguration(
            "ML-KEM", "keyGen", ["ML-KEM-768"]));
        var job = (await accepted.Content.ReadFromJsonAsync<Job>())!;
        var final = await JobPolling.WaitForTerminalAsync(_client, job.JobId);
        final.Status.Should().Be(JobStatus.Succeeded);
        return job.JobId;
    }
}
