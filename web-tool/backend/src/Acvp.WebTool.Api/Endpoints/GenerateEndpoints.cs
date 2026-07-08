using Acvp.WebTool.Api.Models;
using Acvp.WebTool.Api.Services;
using Acvp.WebTool.Api.Validation;

namespace Acvp.WebTool.Api.Endpoints;

/// <summary>T028 — POST /api/generate: enqueue a generation job (202 + Job).</summary>
public static class GenerateEndpoints
{
    public static RouteGroupBuilder MapGenerateEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/generate", (
            AlgorithmConfiguration configuration,
            ConfigurationValidator validator,
            IJobQueue queue,
            GenerateJobHandler handler) =>
        {
            validator.Validate(configuration);

            var job = queue.Enqueue(JobKind.Generate, VsIdProvider.Next(), configuration,
                (createdJob, cancellationToken) => handler.RunAsync(createdJob, configuration, cancellationToken));

            return Results.Accepted($"/api/jobs/{job.JobId}", job);
        });

        return api;
    }
}
