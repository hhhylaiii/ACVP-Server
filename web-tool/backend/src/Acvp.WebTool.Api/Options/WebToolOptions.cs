namespace Acvp.WebTool.Api.Options;

/// <summary>Engine-facing behavior of the tool.</summary>
public sealed class EngineOptions
{
    public const string SectionName = "WebTool:Engine";

    /// <summary>
    /// When true, registrations are built with isSample=true, producing the
    /// smaller sample-sized vector sets suitable for interactive use.
    /// </summary>
    public bool UseSampleVectors { get; init; } = true;
}

/// <summary>Upload and concurrency limits (FR-009, FR-016).</summary>
public sealed class LimitsOptions
{
    public const string SectionName = "WebTool:Limits";

    /// <summary>Maximum accepted response-file upload size in bytes.</summary>
    public long MaxUploadBytes { get; init; } = 50 * 1024 * 1024;

    /// <summary>Maximum generation/validation jobs processed concurrently by the API.</summary>
    public int MaxConcurrentJobs { get; init; } = 2;
}

/// <summary>Filesystem layout for per-job artifacts.</summary>
public sealed class StorageOptions
{
    public const string SectionName = "WebTool:Storage";

    /// <summary>Root directory for per-job artifact folders (relative to content root or absolute).</summary>
    public string ArtifactRoot { get; init; } = "artifacts";
}

/// <summary>Swagger UI exposure. Always on in Development; opt-in elsewhere.</summary>
public sealed class SwaggerOptions
{
    public const string SectionName = "WebTool:Swagger";

    /// <summary>
    /// Expose Swagger UI outside Development (e.g. the Docker self-host).
    /// The tool is fully local, so enabling it does not leak data off-machine.
    /// </summary>
    public bool Enabled { get; init; }
}
