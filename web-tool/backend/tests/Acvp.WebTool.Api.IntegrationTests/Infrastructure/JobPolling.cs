using System.Net.Http.Json;
using Acvp.WebTool.Api.Models;

namespace Acvp.WebTool.Api.IntegrationTests.Infrastructure;

public static class JobPolling
{
    /// <summary>Polls GET /api/jobs/{id} until the job reaches a terminal state.</summary>
    public static async Task<Job> WaitForTerminalAsync(HttpClient client, string jobId, TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));
        while (DateTimeOffset.UtcNow < deadline)
        {
            var job = await client.GetFromJsonAsync<Job>($"/api/jobs/{jobId}");
            if (job!.Status is JobStatus.Succeeded or JobStatus.Failed)
            {
                return job;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException($"Job {jobId} did not reach a terminal state in time.");
    }
}
