using Acvp.WebTool.Api.Models;
using Acvp.WebTool.Api.Options;
using Acvp.WebTool.Api.Services;
using Acvp.WebTool.Api.Validation;
using Microsoft.Extensions.Options;

namespace Acvp.WebTool.Api.Endpoints;

/// <summary>T039 — POST /api/validate: multipart upload of responses.json, enqueue a validate job.</summary>
public static class ValidateEndpoints
{
    public static RouteGroupBuilder MapValidateEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/validate", async (
            HttpRequest request,
            IJobStore store,
            IJobQueue queue,
            IArtifactStore artifacts,
            ResponseUploadValidator uploadValidator,
            ValidateJobHandler handler,
            IOptions<LimitsOptions> limits,
            CancellationToken cancellationToken) =>
        {
            if (!request.HasFormContentType)
            {
                throw WebToolException.BadRequest(SafeErrorCodes.InvalidConfiguration,
                    "The request must be multipart/form-data with a 'jobId' field and a 'responses' file.");
            }

            var form = await request.ReadFormAsync(cancellationToken);
            var generateJobId = form["jobId"].ToString();
            if (string.IsNullOrWhiteSpace(generateJobId))
            {
                throw WebToolException.BadRequest(SafeErrorCodes.MissingField,
                    "The 'jobId' form field identifying the generate job is required.", field: "jobId");
            }

            var file = form.Files["responses"];
            if (file is null || file.Length == 0)
            {
                throw WebToolException.BadRequest(SafeErrorCodes.MissingField,
                    "Attach the responses.json file as the 'responses' form part.", field: "responses");
            }

            if (file.Length > limits.Value.MaxUploadBytes)
            {
                throw WebToolException.UploadTooLarge(limits.Value.MaxUploadBytes);
            }

            var generateJob = store.Get(generateJobId) ?? throw WebToolException.NotFound(generateJobId);
            if (generateJob.Kind != JobKind.Generate || generateJob.Status != JobStatus.Succeeded)
            {
                throw WebToolException.NotReady(generateJobId);
            }

            string responsesJson;
            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                responsesJson = await reader.ReadToEndAsync(cancellationToken);
            }

            // Structural + vector-set match validation before any engine work (FR-008, FR-010).
            var prompt = await artifacts.ReadClientArtifactAsync(generateJobId, ArtifactNames.Prompt, cancellationToken);
            uploadValidator.Validate(responsesJson, prompt);

            var job = queue.Enqueue(JobKind.Validate, generateJob.VsId, generateJob.Configuration,
                (createdJob, ct) => handler.RunAsync(createdJob, generateJobId, responsesJson, ct));

            return Results.Accepted($"/api/jobs/{job.JobId}", job);
        });

        return api;
    }
}
