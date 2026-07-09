using Acvp.WebTool.Api.Models;
using Acvp.WebTool.Api.Services;
using Acvp.WebTool.Api.Validation;

namespace Acvp.WebTool.Api.Endpoints;

/// <summary>T041 — validation report (human-readable) and validation.json (machine-readable).</summary>
public static class ReportEndpoints
{
    public static RouteGroupBuilder MapReportEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/jobs/{jobId}/report", async (
            string jobId,
            IJobStore store,
            IArtifactStore artifacts,
            ValidationReportBuilder reportBuilder,
            CancellationToken cancellationToken) =>
        {
            var validationJson = await ReadValidationJsonAsync(jobId, store, artifacts, cancellationToken);
            return Results.Ok(reportBuilder.Build(jobId, validationJson));
        })
        .WithTags("Reports")
        .WithSummary("Get the human-readable pass/fail report of a succeeded validation job.")
        .Produces<ValidationReport>()
        .Produces<SafeError>(StatusCodes.Status404NotFound)
        .Produces<SafeError>(StatusCodes.Status409Conflict);

        api.MapGet("/jobs/{jobId}/validation-json", async (
            string jobId,
            IJobStore store,
            IArtifactStore artifacts,
            CancellationToken cancellationToken) =>
        {
            var validationJson = await ReadValidationJsonAsync(jobId, store, artifacts, cancellationToken);
            return Results.Text(validationJson, "application/json");
        })
        .WithTags("Reports")
        .WithSummary("Download the machine-readable validation.json of a succeeded validation job.")
        .Produces(StatusCodes.Status200OK, contentType: "application/json")
        .Produces<SafeError>(StatusCodes.Status404NotFound)
        .Produces<SafeError>(StatusCodes.Status409Conflict);

        return api;
    }

    private static async Task<string> ReadValidationJsonAsync(
        string jobId, IJobStore store, IArtifactStore artifacts, CancellationToken cancellationToken)
    {
        var job = store.Get(jobId) ?? throw WebToolException.NotFound(jobId);
        if (job.Kind != JobKind.Validate || job.Status != JobStatus.Succeeded)
        {
            throw WebToolException.NotReady(jobId);
        }

        return await artifacts.ReadClientArtifactAsync(jobId, ArtifactNames.Validation, cancellationToken);
    }
}
