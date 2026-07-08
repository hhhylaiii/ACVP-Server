using Acvp.WebTool.Api.Models;
using Acvp.WebTool.Api.Options;
using Microsoft.Extensions.Options;

namespace Acvp.WebTool.Api.Services;

/// <summary>Allocates unique vector-set ids for generate jobs.</summary>
public static class VsIdProvider
{
    private static long _next = 100;

    public static long Next() => Interlocked.Increment(ref _next);
}

/// <summary>
/// Background work for a generate job: build the registration, run the engine,
/// persist the artifacts (server-only answer key included) and the client-facing
/// example responses + instructions (T028, T030).
/// </summary>
public sealed class GenerateJobHandler
{
    private readonly IGenValService _engine;
    private readonly RegistrationBuilder _registrationBuilder;
    private readonly PromptPackageBuilder _packageBuilder;
    private readonly IArtifactStore _artifacts;
    private readonly bool _useSampleVectors;

    public GenerateJobHandler(
        IGenValService engine,
        RegistrationBuilder registrationBuilder,
        PromptPackageBuilder packageBuilder,
        IArtifactStore artifacts,
        IOptions<EngineOptions> engineOptions)
    {
        _engine = engine;
        _registrationBuilder = registrationBuilder;
        _packageBuilder = packageBuilder;
        _artifacts = artifacts;
        _useSampleVectors = engineOptions.Value.UseSampleVectors;
    }

    public async Task RunAsync(Job job, AlgorithmConfiguration configuration, CancellationToken cancellationToken)
    {
        var registration = _registrationBuilder.Build(configuration, job.VsId, _useSampleVectors);
        var generated = await _engine.GenerateAsync(registration, job.VsId, cancellationToken);

        await _artifacts.SaveAsync(job.JobId, ArtifactNames.Prompt, generated.Prompt, cancellationToken);
        await _artifacts.SaveAsync(job.JobId, ArtifactNames.InternalProjection, generated.InternalProjection, cancellationToken);
        await _artifacts.SaveAsync(job.JobId, ArtifactNames.ExpectedResults, generated.ExpectedResults, cancellationToken);

        var exampleResponses = _packageBuilder.BuildExampleResponses(generated.Prompt, generated.ExpectedResults);
        await _artifacts.SaveAsync(job.JobId, ArtifactNames.ExampleResponses, exampleResponses, cancellationToken);
        await _artifacts.SaveAsync(job.JobId, ArtifactNames.Instructions,
            _packageBuilder.BuildInstructions(configuration), cancellationToken);
    }
}
