using Acvp.WebTool.Api.Models;
using Acvp.WebTool.Api.Services;
using Acvp.WebTool.Api.Validation;

namespace Acvp.WebTool.Api.Endpoints;

/// <summary>T029/T030 — job status polling and the prompt-package download.</summary>
public static class JobsEndpoints
{
    public static RouteGroupBuilder MapJobsEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/jobs/{jobId}", (string jobId, IJobStore store) =>
        {
            var job = store.Get(jobId) ?? throw WebToolException.NotFound(jobId);
            return Results.Ok(job);
        })
        .WithTags("Jobs")
        .WithSummary("Get the status of a generation or validation job.")
        .Produces<Job>()
        .Produces<SafeError>(StatusCodes.Status404NotFound);

        api.MapGet("/jobs/{jobId}/prompt-package", async (
            string jobId,
            IJobStore store,
            IArtifactStore artifacts,
            PromptPackageBuilder packageBuilder,
            CancellationToken cancellationToken) =>
        {
            var job = store.Get(jobId) ?? throw WebToolException.NotFound(jobId);
            if (job.Kind != JobKind.Generate || job.Status != JobStatus.Succeeded)
            {
                throw WebToolException.NotReady(jobId);
            }

            // Client-facing artifacts only; the server-only answer key can never pass this path.
            var prompt = await artifacts.ReadClientArtifactAsync(jobId, ArtifactNames.Prompt, cancellationToken);
            var example = await artifacts.ReadClientArtifactAsync(jobId, ArtifactNames.ExampleResponses, cancellationToken);
            var instructions = await artifacts.ReadClientArtifactAsync(jobId, ArtifactNames.Instructions, cancellationToken);

            var zip = packageBuilder.BuildZip(prompt, example, instructions);
            return Results.File(zip, "application/zip", $"prompt-package-{jobId}.zip");
        })
        .WithTags("Generation")
        .WithSummary("Download the prompt package (prompt.json + example-responses.json + INSTRUCTIONS.md) of a succeeded generation job.")
        .Produces(StatusCodes.Status200OK, contentType: "application/zip")
        .Produces<SafeError>(StatusCodes.Status404NotFound)
        .Produces<SafeError>(StatusCodes.Status409Conflict);

        return api;
    }
}
