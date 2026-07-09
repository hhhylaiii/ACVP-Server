using System.Net;
using System.Net.Http.Json;
using System.Text;
using Acvp.WebTool.Api.IntegrationTests.Infrastructure;
using Acvp.WebTool.Api.Models;
using FluentAssertions;
using Xunit;

namespace Acvp.WebTool.Api.IntegrationTests;

/// <summary>
/// T034 — POST /api/validate multipart upload: 202 + validate Job on success,
/// 413 UPLOAD_TOO_LARGE on oversize, 404 for unknown generate jobs and
/// 400 MISMATCHED_VECTORSET for uploads that answer a different vector set.
/// </summary>
public sealed class ValidateContractTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;
    private readonly HttpClient _client;

    public ValidateContractTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Validate_ExampleResponses_Returns202AndSucceeds()
    {
        var (generateJobId, _) = await GenerateAsync();
        var example = await File.ReadAllTextAsync(
            Path.Combine(_factory.ArtifactRoot, generateJobId, "exampleResponses.json"));

        var response = await PostResponsesAsync(generateJobId, example);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var job = (await response.Content.ReadFromJsonAsync<Job>())!;
        job.Kind.Should().Be(JobKind.Validate);

        var final = await JobPolling.WaitForTerminalAsync(_client, job.JobId);
        final.Status.Should().Be(JobStatus.Succeeded);
    }

    [Fact]
    public async Task Validate_OversizeUpload_Returns413()
    {
        var (generateJobId, _) = await GenerateAsync();
        // Factory limit is 1 MiB; send 2 MiB of padding.
        var oversized = $$"""{ "vsId": 1, "padding": "{{new string('a', 2 * 1024 * 1024)}}" }""";

        var response = await PostResponsesAsync(generateJobId, oversized);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        var error = await response.Content.ReadFromJsonAsync<SafeError>();
        error!.Code.Should().Be(SafeErrorCodes.UploadTooLarge);
    }

    [Fact]
    public async Task Validate_UnknownGenerateJob_Returns404()
    {
        var response = await PostResponsesAsync(Guid.NewGuid().ToString(), """{"vsId":1}""");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<SafeError>();
        error!.Code.Should().Be(SafeErrorCodes.JobNotFound);
    }

    [Fact]
    public async Task Validate_MismatchedVsId_Returns400MismatchedVectorSet()
    {
        var (generateJobId, _) = await GenerateAsync();
        var example = await File.ReadAllTextAsync(
            Path.Combine(_factory.ArtifactRoot, generateJobId, "exampleResponses.json"));

        var response = await PostResponsesAsync(generateJobId, ForceVsId(example, 987654));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<SafeError>();
        error!.Code.Should().Be(SafeErrorCodes.MismatchedVectorSet);
    }

    [Fact]
    public async Task Validate_MissingFilePart_Returns400()
    {
        using var form = new MultipartFormDataContent { { new StringContent("some-job"), "jobId" } };

        var response = await _client.PostAsync("/api/validate", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static string ForceVsId(string json, long vsId)
    {
        var document = Newtonsoft.Json.Linq.JObject.Parse(json);
        document["vsId"] = vsId;
        return document.ToString();
    }

    private async Task<(string JobId, Job Job)> GenerateAsync()
    {
        var accepted = await _client.PostAsJsonAsync("/api/generate", new AlgorithmConfiguration(
            "ML-KEM", "keyGen", ["ML-KEM-768"]));
        var job = (await accepted.Content.ReadFromJsonAsync<Job>())!;
        var final = await JobPolling.WaitForTerminalAsync(_client, job.JobId);
        final.Status.Should().Be(JobStatus.Succeeded);
        return (job.JobId, final);
    }

    private Task<HttpResponseMessage> PostResponsesAsync(string generateJobId, string responsesJson)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(generateJobId), "jobId" },
            { new ByteArrayContent(Encoding.UTF8.GetBytes(responsesJson)), "responses", "responses.json" },
        };
        return _client.PostAsync("/api/validate", form);
    }
}
