using System.Text.Json;
using System.Text.Json.Serialization;

namespace Acvp.WebTool.Api.Models;

/// <summary>Kind of background work. Serialized lowercase per the API contract.</summary>
[JsonConverter(typeof(JobKindJsonConverter))]
public enum JobKind
{
    Generate,
    Validate,
}

/// <summary>Job state machine: Queued → Running → (Succeeded | Failed).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<JobStatus>))]
public enum JobStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
}

/// <summary>
/// An immutable snapshot of a unit of background work (generation or validation).
/// State transitions produce a new record; terminal states are immutable.
/// </summary>
public sealed record Job(
    string JobId,
    long VsId,
    JobKind Kind,
    JobStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt = null,
    SafeError? Error = null,
    [property: JsonIgnore] AlgorithmConfiguration? Configuration = null);

/// <summary>Serializes <see cref="JobKind"/> as lowercase ("generate" / "validate").</summary>
public sealed class JobKindJsonConverter : JsonConverter<JobKind>
{
    public override JobKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Enum.Parse<JobKind>(reader.GetString() ?? string.Empty, ignoreCase: true);

    public override void Write(Utf8JsonWriter writer, JobKind value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString().ToLowerInvariant());
}
