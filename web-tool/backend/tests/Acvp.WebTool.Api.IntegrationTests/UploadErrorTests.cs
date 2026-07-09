using System.Net;
using System.Net.Http.Json;
using System.Text;
using Acvp.WebTool.Api.IntegrationTests.Infrastructure;
using Acvp.WebTool.Api.Models;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Acvp.WebTool.Api.IntegrationTests;

/// <summary>
/// T045 — FR-008/SC-004: malformed uploads produce precise, safe errors naming the
/// offending tcId and field with a hint; never a stack trace.
/// </summary>
public sealed class UploadErrorTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;
    private readonly HttpClient _client;

    public UploadErrorTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Upload_MissingRequiredField_NamesTcIdFieldAndHint()
    {
        var (jobId, example) = await GenerateWithExampleAsync();
        var responses = JObject.Parse(example);
        var secondTest = (JObject)responses["testGroups"]![0]!["tests"]![1]!;
        var removedTcId = secondTest.Value<int>("tcId");
        secondTest.Remove("dk");

        var error = await PostExpectingErrorAsync(jobId, responses.ToString(), HttpStatusCode.BadRequest);

        error.Code.Should().Be(SafeErrorCodes.MissingField);
        error.TcId.Should().Be(removedTcId);
        error.Field.Should().Be("dk");
        error.Hint.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Upload_UnknownTcId_ReportsUnknownTcId()
    {
        var (jobId, example) = await GenerateWithExampleAsync();
        var responses = JObject.Parse(example);
        var firstTest = (JObject)responses["testGroups"]![0]!["tests"]![0]!;
        firstTest["tcId"] = 9999;

        var error = await PostExpectingErrorAsync(jobId, responses.ToString(), HttpStatusCode.BadRequest);

        error.Code.Should().Be(SafeErrorCodes.UnknownTcId);
        error.TcId.Should().Be(9999);
    }

    [Fact]
    public async Task Upload_MalformedJson_ReturnsSafeErrorWithoutStackTrace()
    {
        var (jobId, _) = await GenerateWithExampleAsync();

        var error = await PostExpectingErrorAsync(jobId, "{ not json at all", HttpStatusCode.BadRequest);

        error.Code.Should().Be(SafeErrorCodes.MalformedUpload);
        error.Message.Should().NotContain("   at ");
        error.Message.Should().NotContain("Exception");
    }

    [Fact]
    public async Task Upload_EmptyRequiredField_TreatedAsMissing()
    {
        var (jobId, example) = await GenerateWithExampleAsync();
        var responses = JObject.Parse(example);
        var firstTest = (JObject)responses["testGroups"]![0]!["tests"]![0]!;
        firstTest["ek"] = "";

        var error = await PostExpectingErrorAsync(jobId, responses.ToString(), HttpStatusCode.BadRequest);

        error.Code.Should().Be(SafeErrorCodes.MissingField);
        error.Field.Should().Be("ek");
    }

    private async Task<(string JobId, string Example)> GenerateWithExampleAsync()
    {
        var accepted = await _client.PostAsJsonAsync("/api/generate", new AlgorithmConfiguration(
            "ML-KEM", "keyGen", ["ML-KEM-768"]));
        var job = (await accepted.Content.ReadFromJsonAsync<Job>())!;
        (await JobPolling.WaitForTerminalAsync(_client, job.JobId)).Status.Should().Be(JobStatus.Succeeded);
        var example = await File.ReadAllTextAsync(
            Path.Combine(_factory.ArtifactRoot, job.JobId, "exampleResponses.json"));
        return (job.JobId, example);
    }

    private async Task<SafeError> PostExpectingErrorAsync(string jobId, string responsesJson, HttpStatusCode expected)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(jobId), "jobId" },
            { new ByteArrayContent(Encoding.UTF8.GetBytes(responsesJson)), "responses", "responses.json" },
        };
        var response = await _client.PostAsync("/api/validate", form);
        response.StatusCode.Should().Be(expected);
        return (await response.Content.ReadFromJsonAsync<SafeError>())!;
    }
}
