using Acvp.WebTool.Api.Models;
using NIST.CVP.ACVTS.Libraries.Crypto.Oracle.Exceptions;

namespace Acvp.WebTool.Api.Validation;

/// <summary>
/// Central exception → SafeError/HTTP-status mapping. Guarantees no stack trace or
/// internal detail ever reaches the client (FR-008, Constitution Principle IV).
/// </summary>
public sealed class SafeErrorMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SafeErrorMiddleware> _logger;

    public SafeErrorMiddleware(RequestDelegate next, ILogger<SafeErrorMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (WebToolException ex)
        {
            _logger.LogWarning("Request {Path} rejected: {Code} {Message}",
                context.Request.Path, ex.Error.Code, ex.Error.Message);
            await WriteSafeErrorAsync(context, ex.StatusCode, ex.Error);
        }
        catch (OrleansInitializationException ex)
        {
            _logger.LogError(ex, "Orleans silo unreachable while handling {Path}", context.Request.Path);
            await WriteSafeErrorAsync(context, StatusCodes.Status503ServiceUnavailable, new SafeError(
                SafeErrorCodes.EngineUnavailable,
                "The validation engine is currently unreachable. Please verify the Orleans silo is running and retry."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while handling {Path}", context.Request.Path);
            await WriteSafeErrorAsync(context, StatusCodes.Status500InternalServerError, new SafeError(
                SafeErrorCodes.UnexpectedError,
                "An unexpected error occurred. Check the server logs for details."));
        }
    }

    private static async Task WriteSafeErrorAsync(HttpContext context, int statusCode, SafeError error)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(error);
    }
}
