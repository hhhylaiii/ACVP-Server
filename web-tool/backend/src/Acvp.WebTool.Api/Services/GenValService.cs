using Acvp.WebTool.Api.Models;
using Acvp.WebTool.Api.Validation;
using NIST.CVP.ACVTS.Libraries.Crypto.Oracle.Exceptions;
using NIST.CVP.ACVTS.Libraries.Generation.Core;

namespace Acvp.WebTool.Api.Services;

/// <summary>The three engine artifacts produced by a successful generation run.</summary>
public sealed record GeneratedVectorSet(string Prompt, string InternalProjection, string ExpectedResults);

/// <summary>
/// Thin wrapper around the existing gen-val engine (<see cref="IGenValInvoker"/>).
/// Engine failures are translated into client-safe errors; raw engine messages
/// stay in the server log (FR-008, Principle IV).
/// </summary>
public interface IGenValService
{
    CheckResult CheckParameters(string registrationJson);
    Task<GeneratedVectorSet> GenerateAsync(string registrationJson, long vsId, CancellationToken cancellationToken = default);
    Task<string> ValidateAsync(string internalProjectionJson, string responsesJson, long vsId, CancellationToken cancellationToken = default);
}

public sealed class GenValService : IGenValService
{
    private readonly IGenValInvoker _invoker;
    private readonly ILogger<GenValService> _logger;

    public GenValService(IGenValInvoker invoker, ILogger<GenValService> logger)
    {
        _invoker = invoker;
        _logger = logger;
    }

    public CheckResult CheckParameters(string registrationJson)
    {
        var response = _invoker.CheckParameters(new ParameterCheckRequest(registrationJson));
        return new CheckResult(response.Success, response.ErrorMessage.ToArray());
    }

    public async Task<GeneratedVectorSet> GenerateAsync(string registrationJson, long vsId, CancellationToken cancellationToken = default)
    {
        GenerateResponse response;
        try
        {
            response = await _invoker.GenerateAsync(new GenerateRequest(registrationJson), vsId);
        }
        catch (OrleansInitializationException ex)
        {
            _logger.LogError(ex, "Orleans silo unreachable during generation for vsId {VsId}", vsId);
            throw WebToolException.EngineUnavailable();
        }

        if (!response.Success)
        {
            _logger.LogError("Generation failed for vsId {VsId}: {StatusCode} {ErrorMessage}",
                vsId, response.StatusCode, response.ErrorMessage);
            throw new WebToolException(StatusCodes.Status500InternalServerError, new SafeError(
                SafeErrorCodes.UnexpectedError,
                "Test vector generation failed inside the engine. Check the server logs for details."));
        }

        return new GeneratedVectorSet(response.PromptProjection, response.InternalProjection, response.ResultProjection);
    }

    public async Task<string> ValidateAsync(string internalProjectionJson, string responsesJson, long vsId, CancellationToken cancellationToken = default)
    {
        ValidateResponse response;
        try
        {
            response = await _invoker.ValidateAsync(
                new ValidateRequest(internalProjectionJson, responsesJson, showExpected: false), vsId);
        }
        catch (OrleansInitializationException ex)
        {
            _logger.LogError(ex, "Orleans silo unreachable during validation for vsId {VsId}", vsId);
            throw WebToolException.EngineUnavailable();
        }

        if (!response.Success)
        {
            _logger.LogError("Validation failed for vsId {VsId}: {StatusCode} {ErrorMessage}",
                vsId, response.StatusCode, response.ErrorMessage);
            throw new WebToolException(StatusCodes.Status500InternalServerError, new SafeError(
                SafeErrorCodes.UnexpectedError,
                "Response validation failed inside the engine. Check the server logs for details."));
        }

        return response.ValidationResult;
    }
}
