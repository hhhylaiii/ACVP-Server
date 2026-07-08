using Acvp.WebTool.Api.Models;
using Acvp.WebTool.Api.Options;
using Acvp.WebTool.Api.Services;
using FluentAssertions;
using Xunit;

namespace Acvp.WebTool.Api.UnitTests;

/// <summary>
/// T008 — Job state machine (Queued → Running → Succeeded | Failed, terminal states
/// immutable) and queue concurrency bounded by LimitsOptions.MaxConcurrentJobs (FR-009).
/// </summary>
public sealed class JobQueueStateMachineTests
{
    private static JobQueue CreateQueue(IJobStore store, int maxConcurrentJobs = 2)
        => new(store, Microsoft.Extensions.Options.Options.Create(new LimitsOptions { MaxConcurrentJobs = maxConcurrentJobs }));

    [Fact]
    public void Create_StartsQueued()
    {
        var store = new InMemoryJobStore();

        var job = store.Create(JobKind.Generate, vsId: 42);

        job.Status.Should().Be(JobStatus.Queued);
        job.VsId.Should().Be(42);
        job.Kind.Should().Be(JobKind.Generate);
        job.CompletedAt.Should().BeNull();
        store.Get(job.JobId).Should().Be(job);
    }

    [Fact]
    public void Get_UnknownJob_ReturnsNull()
    {
        new InMemoryJobStore().Get(Guid.NewGuid().ToString()).Should().BeNull();
    }

    [Fact]
    public void Transition_FollowsHappyPath()
    {
        var store = new InMemoryJobStore();
        var job = store.Create(JobKind.Generate, 1);

        store.Transition(job.JobId, JobStatus.Running).Status.Should().Be(JobStatus.Running);
        var done = store.Transition(job.JobId, JobStatus.Succeeded);

        done.Status.Should().Be(JobStatus.Succeeded);
        done.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Transition_QueuedDirectlyToTerminal_Throws()
    {
        var store = new InMemoryJobStore();
        var job = store.Create(JobKind.Generate, 1);

        var act = () => store.Transition(job.JobId, JobStatus.Succeeded);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(JobStatus.Succeeded)]
    [InlineData(JobStatus.Failed)]
    public void Transition_TerminalStatesAreImmutable(JobStatus terminal)
    {
        var store = new InMemoryJobStore();
        var job = store.Create(JobKind.Validate, 1);
        store.Transition(job.JobId, JobStatus.Running);
        store.Transition(job.JobId, terminal,
            terminal == JobStatus.Failed ? new SafeError(SafeErrorCodes.UnexpectedError, "boom") : null);

        foreach (var next in new[] { JobStatus.Queued, JobStatus.Running, JobStatus.Succeeded, JobStatus.Failed })
        {
            var act = () => store.Transition(job.JobId, next);
            act.Should().Throw<InvalidOperationException>($"terminal state {terminal} must not transition to {next}");
        }
    }

    [Fact]
    public void Transition_FailedCarriesSafeError()
    {
        var store = new InMemoryJobStore();
        var job = store.Create(JobKind.Generate, 1);
        store.Transition(job.JobId, JobStatus.Running);

        var failed = store.Transition(job.JobId, JobStatus.Failed, new SafeError(SafeErrorCodes.EngineUnavailable, "engine down"));

        failed.Error.Should().NotBeNull();
        failed.Error!.Code.Should().Be(SafeErrorCodes.EngineUnavailable);
    }

    [Fact]
    public async Task Enqueue_WorkSucceeds_JobEndsSucceeded()
    {
        var store = new InMemoryJobStore();
        var queue = CreateQueue(store);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var processing = queue.ProcessAsync(cts.Token);

        var job = queue.Enqueue(JobKind.Generate, 42, null, _ => Task.CompletedTask);

        await WaitForStatusAsync(store, job.JobId, JobStatus.Succeeded);
        store.Get(job.JobId)!.Status.Should().Be(JobStatus.Succeeded);

        cts.Cancel();
        await SwallowCancellationAsync(processing);
    }

    [Fact]
    public async Task Enqueue_WorkThrows_JobEndsFailedWithSafeErrorAndNoLeakedDetails()
    {
        var store = new InMemoryJobStore();
        var queue = CreateQueue(store);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var processing = queue.ProcessAsync(cts.Token);

        var job = queue.Enqueue(JobKind.Validate, 42, null,
            _ => throw new InvalidOperationException("internal stack detail C:\\secret\\path"));

        await WaitForStatusAsync(store, job.JobId, JobStatus.Failed);
        var failed = store.Get(job.JobId)!;
        failed.Error.Should().NotBeNull();
        failed.Error!.Message.Should().NotContain("secret", "raw exception details must not leak into the safe error");

        cts.Cancel();
        await SwallowCancellationAsync(processing);
    }

    [Fact]
    public async Task ProcessAsync_BoundsConcurrencyToMaxConcurrentJobs()
    {
        const int maxConcurrent = 2;
        const int totalJobs = 5;
        var store = new InMemoryJobStore();
        var queue = CreateQueue(store, maxConcurrent);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var processing = queue.ProcessAsync(cts.Token);

        var running = 0;
        var maxObserved = 0;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var jobs = new List<Job>();
        for (var i = 0; i < totalJobs; i++)
        {
            jobs.Add(queue.Enqueue(JobKind.Generate, i, null, async _ =>
            {
                var now = Interlocked.Increment(ref running);
                InterlockedMax(ref maxObserved, now);
                await gate.Task;
                Interlocked.Decrement(ref running);
            }));
        }

        // Let workers pick up as much as they are allowed to.
        await Task.Delay(500);
        Volatile.Read(ref maxObserved).Should().BeLessThanOrEqualTo(maxConcurrent);
        Volatile.Read(ref maxObserved).Should().BeGreaterThan(0);

        gate.SetResult();
        foreach (var job in jobs)
        {
            await WaitForStatusAsync(store, job.JobId, JobStatus.Succeeded);
        }

        Volatile.Read(ref maxObserved).Should().BeLessThanOrEqualTo(maxConcurrent);

        cts.Cancel();
        await SwallowCancellationAsync(processing);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int snapshot;
        while (value > (snapshot = Volatile.Read(ref target)))
        {
            if (Interlocked.CompareExchange(ref target, value, snapshot) == snapshot)
            {
                break;
            }
        }
    }

    private static async Task WaitForStatusAsync(IJobStore store, string jobId, JobStatus expected)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (store.Get(jobId)?.Status == expected)
            {
                return;
            }

            await Task.Delay(25);
        }

        store.Get(jobId)?.Status.Should().Be(expected, $"job {jobId} should reach {expected} within the timeout");
    }

    private static async Task SwallowCancellationAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }
}
