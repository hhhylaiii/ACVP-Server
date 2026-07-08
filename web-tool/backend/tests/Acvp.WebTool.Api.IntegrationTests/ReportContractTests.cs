using System.Net;
using System.Net.Http.Json;
using System.Text;
using Acvp.WebTool.Api.IntegrationTests.Infrastructure;
using Acvp.WebTool.Api.Models;
using Acvp.WebTool.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Acvp.WebTool.Api.IntegrationTests;

/// <summary>
/// T035 — GET /api/jobs/{id}/report and /validation-json: 200 when Succeeded,
/// 409 JOB_NOT_READY otherwise; failing cases are surfaced with reasons.
/// </summary>
public sealed class ReportContractTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;
    private readonly HttpClient _client;

    public ReportContractTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Report_AllCorrectResponses_ReportsPassed()
    {
        var validateJobId = await RunValidateFlowAsync(corruptOneAnswer: false);

        var report = await _client.GetFromJsonAsync<ValidationReport>($"/api/jobs/{validateJobId}/report");

        report!.Disposition.Should().Be("passed");
        report.Summary.Total.Should().BeGreaterThan(0);
        report.Summary.Failed.Should().Be(0);
        report.Cases.Should().OnlyContain(c => c.Passed);
    }

    [Fact]
    public async Task Report_OneCorruptedAnswer_ReportsFailedWithReason()
    {
        var validateJobId = await RunValidateFlowAsync(corruptOneAnswer: true);

        var report = await _client.GetFromJsonAsync<ValidationReport>($"/api/jobs/{validateJobId}/report");

        report!.Disposition.Should().Be("failed");
        report.Summary.Failed.Should().Be(1);
        var failing = report.Cases.Single(c => !c.Passed);
        failing.Reason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ValidationJson_Succeeded_ReturnsMachineReadableResult()
    {
        var validateJobId = await RunValidateFlowAsync(corruptOneAnswer: false);

        var response = await _client.GetAsync($"/api/jobs/{validateJobId}/validation-json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var validation = JObject.Parse(await response.Content.ReadAsStringAsync());
        validation.Value<string>("disposition").Should().Be("passed");
    }

    [Fact]
    public async Task Report_JobNotSucceeded_Returns409()
    {
        var store = _factory.Services.GetRequiredService<IJobStore>();
        var job = store.Create(JobKind.Validate, vsId: 5);

        var response = await _client.GetAsync($"/api/jobs/{job.JobId}/report");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<SafeError>())!.Code.Should().Be(SafeErrorCodes.JobNotReady);
    }

    [Fact]
    public async Task Report_GenerateJob_Returns409NotAValidationJob()
    {
        var accepted = await _client.PostAsJsonAsync("/api/generate", new AlgorithmConfiguration(
            "ML-KEM", "keyGen", ["ML-KEM-768"]));
        var job = (await accepted.Content.ReadFromJsonAsync<Job>())!;
        await JobPolling.WaitForTerminalAsync(_client, job.JobId);

        var response = await _client.GetAsync($"/api/jobs/{job.JobId}/report");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private async Task<string> RunValidateFlowAsync(bool corruptOneAnswer)
    {
        var accepted = await _client.PostAsJsonAsync("/api/generate", new AlgorithmConfiguration(
            "ML-KEM", "keyGen", ["ML-KEM-768"]));
        var generateJob = (await accepted.Content.ReadFromJsonAsync<Job>())!;
        (await JobPolling.WaitForTerminalAsync(_client, generateJob.JobId)).Status.Should().Be(JobStatus.Succeeded);

        var responses = JObject.Parse(await File.ReadAllTextAsync(
            Path.Combine(_factory.ArtifactRoot, generateJob.JobId, "exampleResponses.json")));

        if (corruptOneAnswer)
        {
            // The fake engine grades a case as failed when it carries __forceFail.
            var firstTest = (JObject)responses["testGroups"]!.First()["tests"]!.First();
            firstTest["__forceFail"] = true;
        }

        var form = new MultipartFormDataContent
        {
            { new StringContent(generateJob.JobId), "jobId" },
            { new ByteArrayContent(Encoding.UTF8.GetBytes(responses.ToString())), "responses", "responses.json" },
        };
        var validateAccepted = await _client.PostAsync("/api/validate", form);
        validateAccepted.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var validateJob = (await validateAccepted.Content.ReadFromJsonAsync<Job>())!;
        (await JobPolling.WaitForTerminalAsync(_client, validateJob.JobId)).Status.Should().Be(JobStatus.Succeeded);

        return validateJob.JobId;
    }
}
