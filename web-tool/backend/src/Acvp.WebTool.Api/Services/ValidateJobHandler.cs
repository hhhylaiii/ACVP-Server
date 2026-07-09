using Acvp.WebTool.Api.Models;

namespace Acvp.WebTool.Api.Services;

/// <summary>
/// Background work for a validate job: read the server-only internalProjection of
/// the originating generate job, run the engine validator, persist validation.json
/// (T039). The answer key never leaves the server.
/// </summary>
public sealed class ValidateJobHandler
{
    private readonly IGenValService _engine;
    private readonly IArtifactStore _artifacts;

    public ValidateJobHandler(IGenValService engine, IArtifactStore artifacts)
    {
        _engine = engine;
        _artifacts = artifacts;
    }

    public async Task RunAsync(Job job, string generateJobId, string responsesJson, CancellationToken cancellationToken)
    {
        await _artifacts.SaveAsync(job.JobId, ArtifactNames.Responses, responsesJson, cancellationToken);

        var internalProjection = await _artifacts.ReadServerOnlyArtifactAsync(
            generateJobId, ArtifactNames.InternalProjection, cancellationToken);

        var validationJson = await _engine.ValidateAsync(internalProjection, responsesJson, job.VsId, cancellationToken);

        await _artifacts.SaveAsync(job.JobId, ArtifactNames.Validation, validationJson, cancellationToken);
    }
}
