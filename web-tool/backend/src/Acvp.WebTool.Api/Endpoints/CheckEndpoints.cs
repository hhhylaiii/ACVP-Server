using Acvp.WebTool.Api.Models;
using Acvp.WebTool.Api.Options;
using Acvp.WebTool.Api.Services;
using Acvp.WebTool.Api.Validation;
using Microsoft.Extensions.Options;

namespace Acvp.WebTool.Api.Endpoints;

/// <summary>T027 — POST /api/check: synchronous configuration validation.</summary>
public static class CheckEndpoints
{
    public static RouteGroupBuilder MapCheckEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/check", (
            AlgorithmConfiguration configuration,
            ConfigurationValidator validator,
            RegistrationBuilder registrationBuilder,
            IGenValService engine,
            IOptions<EngineOptions> engineOptions) =>
        {
            validator.Validate(configuration);
            var registration = registrationBuilder.Build(configuration, vsId: 1, engineOptions.Value.UseSampleVectors);
            return Results.Ok(engine.CheckParameters(registration));
        });

        return api;
    }
}
