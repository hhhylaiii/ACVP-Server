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
    private static readonly HashSet<string> KnownArtifacts = new(StringComparer.Ordinal)
    {
        ArtifactNames.Prompt,
        ArtifactNames.InternalProjection,
        ArtifactNames.ExpectedResults,
        ArtifactNames.ExampleResponses,
        ArtifactNames.Instructions,
        ArtifactNames.Responses,
        ArtifactNames.Validation,
    };

    private static readonly HashSet<string> ServerOnlyArtifacts = new(StringComparer.Ordinal)
    {
        ArtifactNames.InternalProjection,
        ArtifactNames.ExpectedResults,
    };

    private readonly string _root;

    public ArtifactStore(IOptions<StorageOptions> options)
    {
        _root = Path.GetFullPath(options.Value.ArtifactRoot);
    }

    public async Task SaveAsync(string jobId, string artifactName, string content, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(jobId, artifactName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content, cancellationToken);
    }

    public Task<string> ReadClientArtifactAsync(string jobId, string artifactName, CancellationToken cancellationToken = default)
    {
        if (ServerOnlyArtifacts.Contains(artifactName))
        {
            throw new InvalidOperationException(
                $"Artifact '{artifactName}' is server-only and must never be exposed to the client.");
        }

        return ReadAsync(jobId, artifactName, cancellationToken);
    }

    public Task<string> ReadServerOnlyArtifactAsync(string jobId, string artifactName, CancellationToken cancellationToken = default)
        => ReadAsync(jobId, artifactName, cancellationToken);

    public bool Exists(string jobId, string artifactName)
        => File.Exists(ResolvePath(jobId, artifactName));

    private Task<string> ReadAsync(string jobId, string artifactName, CancellationToken cancellationToken)
    {
        var path = ResolvePath(jobId, artifactName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Artifact '{artifactName}' does not exist for job '{jobId}'.", path);
        }

        return File.ReadAllTextAsync(path, cancellationToken);
    }

    private string ResolvePath(string jobId, string artifactName)
    {
        if (!Guid.TryParseExact(jobId, "D", out _))
        {
            throw new ArgumentException("Job id must be a GUID.", nameof(jobId));
        }

        if (!KnownArtifacts.Contains(artifactName))
        {
            throw new ArgumentException($"Unknown artifact name '{artifactName}'.", nameof(artifactName));
        }

        return Path.Combine(_root, jobId, artifactName);
    }
}
