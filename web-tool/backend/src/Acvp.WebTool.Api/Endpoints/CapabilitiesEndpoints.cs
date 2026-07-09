using Acvp.WebTool.Api.Models;
using Acvp.WebTool.Api.Validation;

namespace Acvp.WebTool.Api.Endpoints;

/// <summary>T026 — GET /api/capabilities (FR-001, FR-002).</summary>
public static class CapabilitiesEndpoints
{
    public static RouteGroupBuilder MapCapabilitiesEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/capabilities", (ConfigurationValidator validator)
            => Results.Ok(validator.GetCapabilities()))
            .WithTags("Capabilities")
            .WithSummary("List the supported algorithm / mode / parameter-set matrix.")
            .Produces<Capabilities>();

        return api;
    }
}
