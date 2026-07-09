using System.Net;
using System.Net.Http.Json;
using Acvp.WebTool.Api.IntegrationTests.Infrastructure;
using Acvp.WebTool.Api.Models;
using FluentAssertions;
using Xunit;

namespace Acvp.WebTool.Api.IntegrationTests;

/// <summary>T019 — POST /api/generate returns 202 with a Job; GET /api/jobs/{id} polls status.</summary>
public sealed class GenerateJobContractTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;

    public GenerateJobContractTests(TestWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Generate_ValidConfiguration_Returns202AcceptedJob()
    {
        var response = await _client.PostAsJsonAsync("/api/generate", new AlgorithmConfiguration(
            "ML-KEM", "keyGen", ["ML-KEM-768"]));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var job = await response.Content.ReadFromJsonAsync<Job>();
        job!.JobId.Should().NotBeNullOrWhiteSpace();
        job.Kind.Should().Be(JobKind.Generate);
        job.Status.Should().BeOneOf(JobStatus.Queued, JobStatus.Running);
        job.VsId.Should().BePositive();
    }

    [Fact]
    public async Task Generate_ThenPoll_ReachesSucceeded()
    {
        var accepted = await _client.PostAsJsonAsync("/api/generate", new AlgorithmConfiguration(
            "ML-DSA", "keyGen", ["ML-DSA-65"]));
        var job = (await accepted.Content.ReadFromJsonAsync<Job>())!;

        var final = await JobPolling.WaitForTerminalAsync(_client, job.JobId);

        final.Status.Should().Be(JobStatus.Succeeded);
        final.CompletedAt.Should().NotBeNull();
        final.Error.Should().BeNull();
    }

    [Fact]
    public async Task Generate_UnsupportedSelection_Returns400WithoutCreatingJob()
    {
        var response = await _client.PostAsJsonAsync("/api/generate", new AlgorithmConfiguration(
            "ML-KEM", "sigVer", ["ML-KEM-768"]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<SafeError>();
        error!.Code.Should().Be(SafeErrorCodes.UnsupportedSelection);
    }

    [Fact]
    public async Task GetJob_UnknownJobId_Returns404JobNotFound()
    {
        var response = await _client.GetAsync($"/api/jobs/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<SafeError>();
        error!.Code.Should().Be(SafeErrorCodes.JobNotFound);
    }
}
