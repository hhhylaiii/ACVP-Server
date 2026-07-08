using Microsoft.Extensions.Options;
using Acvp.WebTool.Api.Options;

namespace Acvp.WebTool.Api.Services;

/// <summary>Well-known artifact file names within a job directory.</summary>
public static class ArtifactNames
{
    public const string Prompt = "prompt.json";
    public const string InternalProjection = "internalProjection.json";
    public const string ExpectedResults = "expectedResults.json";
    public const string ExampleResponses = "exampleResponses.json";
    public const string Instructions = "instructions.md";
    public const string Responses = "responses.json";
    public const string Validation = "validation.json";
}

/// <summary>
/// Per-job filesystem artifact storage. Enforces the data-privacy boundary:
/// <c>internalProjection.json</c> and <c>expectedResults.json</c> are server-only
/// and can never be read through the client-facing accessor (FR-011, R5).
/// </summary>
public interface IArtifactStore
{
    Task SaveAsync(string jobId, string artifactName, string content, CancellationToken cancellationToken = default);

    /// <summary>Reads an artifact that may be returned to the client. Refuses server-only artifacts.</summary>
    Task<string> ReadClientArtifactAsync(string jobId, string artifactName, CancellationToken cancellationToken = default);

    /// <summary>Server-side read path; never wired to any HTTP response for server-only artifacts.</summary>
    Task<string> ReadServerOnlyArtifactAsync(string jobId, string artifactName, CancellationToken cancellationToken = default);

    bool Exists(string jobId, string artifactName);
}

public sealed class ArtifactStore : IArtifactStore
{
    public ArtifactStore(IOptions<StorageOptions> options)
    {
        _ = options;
    }

    public Task SaveAsync(string jobId, string artifactName, string content, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<string> ReadClientArtifactAsync(string jobId, string artifactName, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<string> ReadServerOnlyArtifactAsync(string jobId, string artifactName, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public bool Exists(string jobId, string artifactName)
        => throw new NotImplementedException();
}
