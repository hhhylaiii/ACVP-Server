using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using Acvp.WebTool.Api.IntegrationTests.Infrastructure;
using Acvp.WebTool.Api.Models;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Acvp.WebTool.Api.IntegrationTests;

/// <summary>
/// T037 — FR-007/SC-003 golden parity across ALL five supported modes using the
/// REAL engine: correct responses grade "passed", a single corrupted answer flips
/// the verdict to a non-passed disposition, and (for ML-KEM keyGen) the CLI
/// oracle's validation.json disposition matches the API's report exactly.
/// Requires ACVP_WEBTOOL_GOLDEN_PARITY=1 and a running Orleans silo.
/// </summary>
public sealed class ValidateGoldenParityTests : IClassFixture<RealEngineWebAppFactory>
{
    private static readonly TimeSpan JobTimeout = TimeSpan.FromMinutes(15);

    private readonly RealEngineWebAppFactory _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    public ValidateGoldenParityTests(RealEngineWebAppFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _output = output;
    }

    [SiloTheory]
    [InlineData("ML-KEM", "keyGen", "ML-KEM-768")]
    [InlineData("ML-KEM", "encapDecap", "ML-KEM-768")]
    [InlineData("ML-DSA", "keyGen", "ML-DSA-65")]
    [InlineData("ML-DSA", "sigGen", "ML-DSA-65")]
    [InlineData("ML-DSA", "sigVer", "ML-DSA-65")]
    public async Task Validate_CorrectAndCorruptedAnswers_MatchEngineVerdicts(
        string algorithm, string mode, string parameterSet)
    {
        var generateJobId = await GenerateAsync(algorithm, mode, parameterSet);
        var example = await File.ReadAllTextAsync(
            Path.Combine(_factory.ArtifactRoot, generateJobId, "exampleResponses.json"));

        // 1) The example (all-correct) responses must grade "passed".
        var passedReport = await ValidateAsync(generateJobId, example);
        passedReport.Disposition.Should().Be("passed",
            $"{algorithm}/{mode}: correct answers must pass");
        passedReport.Summary.Failed.Should().Be(0);

        // 2) Corrupting exactly one answer must flip the verdict away from "passed".
        var corrupted = CorruptFirstAnswer(example);
        var failedReport = await ValidateAsync(generateJobId, corrupted);
        failedReport.Disposition.Should().NotBe("passed",
            $"{algorithm}/{mode}: a corrupted answer must be caught");
        failedReport.Cases.Should().Contain(c => !c.Passed);

        _output.WriteLine($"{algorithm}/{mode}: passed-parity ok, corrupted disposition = {failedReport.Disposition}");
    }

    [SiloFact]
    public async Task Validate_MlKemKeyGen_DispositionMatchesCliOracle()
    {
        var generateJobId = await GenerateAsync("ML-KEM", "keyGen", "ML-KEM-768");
        var artifactDir = Path.Combine(_factory.ArtifactRoot, generateJobId);
        var example = await File.ReadAllTextAsync(Path.Combine(artifactDir, "exampleResponses.json"));

        var apiReport = await ValidateAsync(generateJobId, example);

        // CLI oracle: GenValAppRunner -n internalProjection.json -b responses.json
        var cliDir = Path.Combine(Path.GetTempPath(), $"acvp-cli-oracle-{Guid.NewGuid():N}");
        Directory.CreateDirectory(cliDir);
        try
        {
            File.Copy(Path.Combine(artifactDir, "internalProjection.json"), Path.Combine(cliDir, "internalProjection.json"));
            await File.WriteAllTextAsync(Path.Combine(cliDir, "responses.json"), example);

            var runnerProject = Path.Combine(FindRepoRoot(), "gen-val", "samples", "GenValAppRunner", "src");
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{runnerProject}\" -- -n \"{Path.Combine(cliDir, "internalProjection.json")}\" -b \"{Path.Combine(cliDir, "responses.json")}\"",
                WorkingDirectory = cliDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            })!;
            await process.WaitForExitAsync(new CancellationTokenSource(JobTimeout).Token);

            var cliValidation = JObject.Parse(await File.ReadAllTextAsync(Path.Combine(cliDir, "validation.json")));
            cliValidation.Value<string>("disposition").Should().Be(apiReport.Disposition,
                "the tool's verdict must equal the reference CLI workflow (FR-007)");
        }
        finally
        {
            Directory.Delete(cliDir, recursive: true);
        }
    }

    private static string CorruptFirstAnswer(string responsesJson)
    {
        var responses = JObject.Parse(responsesJson);
        var firstTest = (JObject)responses["testGroups"]!.First(g => g["tests"]!.Any())["tests"]!.First();
        var answer = firstTest.Properties().First(p => p.Name != "tcId");

        firstTest[answer.Name] = answer.Value.Type switch
        {
            JTokenType.Boolean => !answer.Value.Value<bool>(),
            JTokenType.String when answer.Value.Value<string>()!.Length >= 2 =>
                (answer.Value.Value<string>()!.StartsWith("00") ? "ff" : "00") + answer.Value.Value<string>()![2..],
            _ => "00",
        };

        return responses.ToString();
    }

    private async Task<string> GenerateAsync(string algorithm, string mode, string parameterSet)
    {
        var accepted = await _client.PostAsJsonAsync("/api/generate", new AlgorithmConfiguration(
            algorithm, mode, [parameterSet]));
        accepted.EnsureSuccessStatusCode();
        var job = (await accepted.Content.ReadFromJsonAsync<Job>())!;
        var final = await JobPolling.WaitForTerminalAsync(_client, job.JobId, JobTimeout);
        final.Status.Should().Be(JobStatus.Succeeded, $"generation failed: {final.Error?.Message}");
        return job.JobId;
    }

    private async Task<ValidationReport> ValidateAsync(string generateJobId, string responsesJson)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(generateJobId), "jobId" },
            { new ByteArrayContent(Encoding.UTF8.GetBytes(responsesJson)), "responses", "responses.json" },
        };
        var accepted = await _client.PostAsync("/api/validate", form);
        accepted.EnsureSuccessStatusCode();
        var job = (await accepted.Content.ReadFromJsonAsync<Job>())!;
        var final = await JobPolling.WaitForTerminalAsync(_client, job.JobId, JobTimeout);
        final.Status.Should().Be(JobStatus.Succeeded, $"validation failed: {final.Error?.Message}");

        return (await _client.GetFromJsonAsync<ValidationReport>($"/api/jobs/{job.JobId}/report"))!;
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "gen-val")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root (gen-val directory).");
    }
}
