using Acvp.WebTool.Api.Models;

namespace Acvp.WebTool.Api.Validation;

/// <summary>
/// Application exception carrying a client-safe error and the HTTP status to map it to.
/// Thrown anywhere below the endpoint layer; translated by <c>SafeErrorMiddleware</c>.
/// </summary>
public sealed class WebToolException : Exception
{
    public SafeError Error { get; }
    public int StatusCode { get; }

    public WebToolException(int statusCode, SafeError error) : base(error.Message)
    {
        StatusCode = statusCode;
        Error = error;
    }

    public static WebToolException BadRequest(string code, string message, int? tcId = null, string? field = null, string? hint = null)
        => new(StatusCodes.Status400BadRequest, new SafeError(code, message, tcId, field, hint));

    public static WebToolException NotFound(string jobId)
        => new(StatusCodes.Status404NotFound, new SafeError(SafeErrorCodes.JobNotFound, $"Job '{jobId}' was not found."));

    public static WebToolException NotReady(string jobId)
        => new(StatusCodes.Status409Conflict, new SafeError(SafeErrorCodes.JobNotReady, $"Job '{jobId}' has not completed successfully yet."));

    public static WebToolException UploadTooLarge(long limitBytes)
        => new(StatusCodes.Status413PayloadTooLarge, new SafeError(
            SafeErrorCodes.UploadTooLarge,
            $"The uploaded file exceeds the configured limit of {limitBytes} bytes."));

    public static WebToolException EngineUnavailable()
        => new(StatusCodes.Status503ServiceUnavailable, new SafeError(
            SafeErrorCodes.EngineUnavailable,
            "The validation engine is currently unreachable. Please verify the Orleans silo is running and retry."));
}
