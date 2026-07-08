using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Acvp.WebTool.Api.Models;
using Acvp.WebTool.Api.Options;
using Acvp.WebTool.Api.Validation;

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

    /// <summary>Overload receiving the created job so the work can reference its id.</summary>
    Job Enqueue(JobKind kind, long vsId, AlgorithmConfiguration? configuration, Func<Job, CancellationToken, Task> work);
}

public sealed class InMemoryJobStore : IJobStore
{
    private readonly ConcurrentDictionary<string, Job> _jobs = new();
    private readonly object _transitionLock = new();

    public Job Create(JobKind kind, long vsId, AlgorithmConfiguration? configuration = null)
    {
        var job = new Job(
            JobId: Guid.NewGuid().ToString(),
            VsId: vsId,
            Kind: kind,
            Status: JobStatus.Queued,
            CreatedAt: DateTimeOffset.UtcNow,
            Configuration: configuration);

        _jobs[job.JobId] = job;
        return job;
    }

    public Job? Get(string jobId)
        => _jobs.TryGetValue(jobId, out var job) ? job : null;

    public Job Transition(string jobId, JobStatus to, SafeError? error = null)
    {
        lock (_transitionLock)
        {
            var current = Get(jobId)
                ?? throw new InvalidOperationException($"Job '{jobId}' does not exist.");

            if (!IsAllowed(current.Status, to))
            {
                throw new InvalidOperationException(
                    $"Illegal job transition {current.Status} -> {to} for job '{jobId}'.");
            }

            var updated = current with
            {
                Status = to,
                CompletedAt = IsTerminal(to) ? DateTimeOffset.UtcNow : null,
                Error = to == JobStatus.Failed ? error : null,
            };

            _jobs[jobId] = updated;
            return updated;
        }
    }

    private static bool IsTerminal(JobStatus status)
        => status is JobStatus.Succeeded or JobStatus.Failed;

    private static bool IsAllowed(JobStatus from, JobStatus to) => (from, to) switch
    {
        (JobStatus.Queued, JobStatus.Running) => true,
        (JobStatus.Running, JobStatus.Succeeded) => true,
        (JobStatus.Running, JobStatus.Failed) => true,
        _ => false,
    };
}

public sealed class JobQueue : IJobQueue
{
    private readonly IJobStore _jobStore;
    private readonly int _maxConcurrentJobs;
    private readonly ILogger<JobQueue>? _logger;
    private readonly Channel<(string JobId, Func<CancellationToken, Task> Work)> _channel =
        Channel.CreateUnbounded<(string, Func<CancellationToken, Task>)>();

    public JobQueue(IJobStore jobStore, IOptions<LimitsOptions> limits, ILogger<JobQueue>? logger = null)
    {
        _jobStore = jobStore;
        _maxConcurrentJobs = Math.Max(1, limits.Value.MaxConcurrentJobs);
        _logger = logger;
    }

    public Job Enqueue(JobKind kind, long vsId, AlgorithmConfiguration? configuration, Func<CancellationToken, Task> work)
        => Enqueue(kind, vsId, configuration, (_, cancellationToken) => work(cancellationToken));

    public Job Enqueue(JobKind kind, long vsId, AlgorithmConfiguration? configuration, Func<Job, CancellationToken, Task> work)
    {
        var job = _jobStore.Create(kind, vsId, configuration);
        if (!_channel.Writer.TryWrite((job.JobId, cancellationToken => work(job, cancellationToken))))
        {
            throw new InvalidOperationException("The job queue is no longer accepting work.");
        }

        return job;
    }

    /// <summary>Processes queued jobs until cancelled; run by the hosted worker service.</summary>
    public Task ProcessAsync(CancellationToken cancellationToken)
    {
        var workers = Enumerable
            .Range(0, _maxConcurrentJobs)
            .Select(_ => WorkerLoopAsync(cancellationToken));

        return Task.WhenAll(workers);
    }

    private async Task WorkerLoopAsync(CancellationToken cancellationToken)
    {
        while (await _channel.Reader.WaitToReadAsync(cancellationToken))
        {
            while (_channel.Reader.TryRead(out var item))
            {
                await RunJobAsync(item.JobId, item.Work, cancellationToken);
            }
        }
    }

    private async Task RunJobAsync(string jobId, Func<CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        _jobStore.Transition(jobId, JobStatus.Running);
        try
        {
            await work(cancellationToken);
            _jobStore.Transition(jobId, JobStatus.Succeeded);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _jobStore.Transition(jobId, JobStatus.Failed, new SafeError(
                SafeErrorCodes.UnexpectedError, "The job was cancelled because the server is shutting down."));
            throw;
        }
        catch (WebToolException ex)
        {
            _logger?.LogWarning(ex, "Job {JobId} failed with safe error {Code}", jobId, ex.Error.Code);
            _jobStore.Transition(jobId, JobStatus.Failed, ex.Error);
        }
        catch (Exception ex)
        {
            // Full details stay in the server log; only a generic message is exposed.
            _logger?.LogError(ex, "Job {JobId} failed unexpectedly", jobId);
            _jobStore.Transition(jobId, JobStatus.Failed, new SafeError(
                SafeErrorCodes.UnexpectedError,
                "The job failed unexpectedly. Check the server logs for details."));
        }
    }
}

/// <summary>Hosted worker that drains the job queue for the lifetime of the app.</summary>
public sealed class JobWorkerService : BackgroundService
{
    private readonly JobQueue _queue;
    private readonly ILogger<JobWorkerService> _logger;

    public JobWorkerService(JobQueue queue, ILogger<JobWorkerService> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _queue.ProcessAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Job worker stopped.");
        }
    }
}
