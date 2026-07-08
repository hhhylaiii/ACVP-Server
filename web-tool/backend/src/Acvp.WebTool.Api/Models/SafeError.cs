namespace Acvp.WebTool.Api.Models;

/// <summary>
/// Stable machine-readable error codes exposed to the client (contracts/openapi.yaml).
/// </summary>
public static class SafeErrorCodes
{
    public const string InvalidConfiguration = "INVALID_CONFIGURATION";
    public const string UnsupportedSelection = "UNSUPPORTED_SELECTION";
    public const string MissingField = "MISSING_FIELD";
    public const string UnknownTcId = "UNKNOWN_TCID";
    public const string MismatchedVectorSet = "MISMATCHED_VECTORSET";
    public const string UploadTooLarge = "UPLOAD_TOO_LARGE";
    public const string EngineUnavailable = "ENGINE_UNAVAILABLE";
    public const string JobNotFound = "JOB_NOT_FOUND";
    public const string JobNotReady = "JOB_NOT_READY";
    public const string UnexpectedError = "UNEXPECTED_ERROR";
    public const string MalformedUpload = "MALFORMED_UPLOAD";
}

/// <summary>
/// A safe, non-leaking error surfaced to the client. Never carries stack traces
/// or internal engine details (FR-008, Constitution Principle IV).
/// </summary>
public sealed record SafeError(
    string Code,
    string Message,
    int? TcId = null,
    string? Field = null,
    string? Hint = null);
