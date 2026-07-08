using Microsoft.Extensions.Options;
using Acvp.WebTool.Api.Models;
using Acvp.WebTool.Api.Options;

namespace Acvp.WebTool.Api.Services;

/// <summary>In-memory registry of jobs enforcing the Queued → Running → (Succeeded | Failed) state machine.</summary>
public interface IJobStore
{
    Job Create(JobKind kind, long vsId, AlgorithmConfiguration? configuration = null);
    Job? Get(string jobId);

    /// <summary>
    /// Transitions a job to a new status, returning the updated snapshot.
    /// Throws <see cref="InvalidOperationException"/> on any transition outside the
    /// state machine; terminal states are immutable.
    /// </summary>
    Job Transition(string jobId, JobStatus to, SafeError? error = null);
}

/// <summary>Queues background work bounded by <see cref="LimitsOptions.MaxConcurrentJobs"/> (FR-009).</summary>
public interface IJobQueue
{
    Job Enqueue(JobKind kind, long vsId, AlgorithmConfiguration? configuration, Func<CancellationToken, Task> work);
}

public sealed class InMemoryJobStore : IJobStore
{
    public Job Create(JobKind kind, long vsId, AlgorithmConfiguration? configuration = null)
        => throw new NotImplementedException();

    public Job? Get(string jobId)
        => throw new NotImplementedException();

    public Job Transition(string jobId, JobStatus to, SafeError? error = null)
        => throw new NotImplementedException();
}

public sealed class JobQueue : IJobQueue
{
    public JobQueue(IJobStore jobStore, IOptions<LimitsOptions> limits)
    {
        _ = jobStore;
        _ = limits;
    }

    public Job Enqueue(JobKind kind, long vsId, AlgorithmConfiguration? configuration, Func<CancellationToken, Task> work)
        => throw new NotImplementedException();

    /// <summary>Processes queued jobs until cancelled; run by the hosted worker service.</summary>
    public Task ProcessAsync(CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
