using System.Net;
using System.Net.Http.Json;
using Acvp.WebTool.Api.IntegrationTests.Infrastructure;
using Acvp.WebTool.Api.Models;
using Acvp.WebTool.Api.Services;
using Acvp.WebTool.Api.Validation;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NIST.CVP.ACVTS.Libraries.Crypto.Oracle.Exceptions;
using Xunit;

namespace Acvp.WebTool.Api.IntegrationTests;

/// <summary>
/// T055 — when the Orleans silo is unreachable the operator sees a clear,
/// non-sensitive ENGINE_UNAVAILABLE error with retry guidance; jobs fail safely.
/// </summary>
public sealed class EngineUnavailableTests : IClassFixture<EngineUnavailableTests.EngineDownFactory>
{
    private readonly HttpClient _client;

    public EngineUnavailableTests(EngineDownFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Generate_EngineDown_JobFailsWithEngineUnavailable()
    {
        var accepted = await _client.PostAsJsonAsync("/api/generate", new AlgorithmConfiguration(
            "ML-KEM", "keyGen", ["ML-KEM-768"]));
        accepted.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var job = (await accepted.Content.ReadFromJsonAsync<Job>())!;

        var final = await JobPolling.WaitForTerminalAsync(_client, job.JobId);

        final.Status.Should().Be(JobStatus.Failed);
        final.Error!.Code.Should().Be(SafeErrorCodes.EngineUnavailable);
        final.Error.Message.Should().Contain("retry", "the operator needs retry guidance");
        final.Error.Message.Should().NotContain("Exception");
    }

    [Fact]
    public async Task Check_EngineDown_Returns503SafeError()
    {
        var response = await _client.PostAsJsonAsync("/api/check", new AlgorithmConfiguration(
            "ML-KEM", "keyGen", ["ML-KEM-768"]));

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var error = await response.Content.ReadFromJsonAsync<SafeError>();
        error!.Code.Should().Be(SafeErrorCodes.EngineUnavailable);
        error.Message.Should().NotContain("   at ");
    }

    /// <summary>Engine stand-in that behaves like an unreachable Orleans silo.</summary>
    private sealed class EngineDownGenValService : IGenValService
    {
        public CheckResult CheckParameters(string registrationJson)
            => throw new OrleansInitializationException();

        public Task<GeneratedVectorSet> GenerateAsync(string registrationJson, long vsId, CancellationToken cancellationToken = default)
            => throw WebToolException.EngineUnavailable();

        public Task<string> ValidateAsync(string internalProjectionJson, string responsesJson, long vsId, CancellationToken cancellationToken = default)
            => throw WebToolException.EngineUnavailable();
    }

    public sealed class EngineDownFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll(typeof(IGenValService));
                services.AddSingleton<IGenValService, EngineDownGenValService>();
            });
        }
    }
}
