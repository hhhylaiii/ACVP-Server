using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using Acvp.WebTool.Api.IntegrationTests.Infrastructure;
using Acvp.WebTool.Api.Models;
using Acvp.WebTool.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Acvp.WebTool.Api.IntegrationTests;

/// <summary>
/// T020 — GET /api/jobs/{id}/prompt-package: 200 zip with prompt + matching example
/// responses + instructions when Succeeded; 409 JOB_NOT_READY; 404 JOB_NOT_FOUND.
/// </summary>
public sealed class PromptPackageContractTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;
    private readonly HttpClient _client;

    public PromptPackageContractTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PromptPackage_SucceededJob_ReturnsZipWithPromptExampleAndInstructions()
    {
        var accepted = await _client.PostAsJsonAsync("/api/generate", new AlgorithmConfiguration(
            "ML-KEM", "keyGen", ["ML-KEM-768"]));
        var job = (await accepted.Content.ReadFromJsonAsync<Job>())!;
        await JobPolling.WaitForTerminalAsync(_client, job.JobId);

        var response = await _client.GetAsync($"/api/jobs/{job.JobId}/prompt-package");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/zip");

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        var entryNames = zip.Entries.Select(e => e.Name).ToArray();

        entryNames.Should().Contain("prompt.json");
        entryNames.Should().Contain("example-responses.json");
        entryNames.Should().Contain("INSTRUCTIONS.md");

        var prompt = JObject.Parse(await ReadEntryAsync(zip, "prompt.json"));
        var example = JObject.Parse(await ReadEntryAsync(zip, "example-responses.json"));

        // The example answers every prompt case with the same tcIds (FR-004).
        prompt["vsId"].Should().NotBeNull();
        example["vsId"]!.Value<long>().Should().Be(prompt["vsId"]!.Value<long>());
        var promptTcIds = prompt["testGroups"]!.SelectMany(g => g["tests"]!).Select(t => t.Value<int>("tcId"));
        var exampleTcIds = example["testGroups"]!.SelectMany(g => g["tests"]!).Select(t => t.Value<int>("tcId"));
        exampleTcIds.Should().BeEquivalentTo(promptTcIds);

        var instructions = await ReadEntryAsync(zip, "INSTRUCTIONS.md");
        instructions.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PromptPackage_JobNotSucceededYet_Returns409JobNotReady()
    {
        // Create a job directly in the store so its state is deterministically Queued.
        var store = _factory.Services.GetRequiredService<IJobStore>();
        var job = store.Create(JobKind.Generate, vsId: 7);

        var response = await _client.GetAsync($"/api/jobs/{job.JobId}/prompt-package");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await response.Content.ReadFromJsonAsync<SafeError>();
        error!.Code.Should().Be(SafeErrorCodes.JobNotReady);
    }

    [Fact]
    public async Task PromptPackage_UnknownJob_Returns404()
    {
        var response = await _client.GetAsync($"/api/jobs/{Guid.NewGuid()}/prompt-package");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<SafeError>();
        error!.Code.Should().Be(SafeErrorCodes.JobNotFound);
    }

    private static async Task<string> ReadEntryAsync(ZipArchive zip, string name)
    {
        var entry = zip.Entries.Single(e => e.Name == name);
        using var reader = new StreamReader(entry.Open());
        return await reader.ReadToEndAsync();
    }
}
