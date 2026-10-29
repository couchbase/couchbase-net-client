using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Utils;
using Xunit;

namespace Couchbase.UnitTests.Utils;

public class TaskHelpersTests
{
    public enum TaskResult
    {
        Success,
        Failure,
        NeverCompletes
    }

    [Theory]
    [InlineData(new[] { TaskResult.Failure, TaskResult.Failure, TaskResult.Failure, TaskResult.Failure, TaskResult.Success })]
    [InlineData(new[] { TaskResult.Failure, TaskResult.Failure, TaskResult.Failure, TaskResult.Success, TaskResult.Failure })]
    [InlineData(new[] { TaskResult.Failure, TaskResult.Success, TaskResult.Failure, TaskResult.Success, TaskResult.Failure })]
    [InlineData(new[] { TaskResult.NeverCompletes, TaskResult.NeverCompletes, TaskResult.NeverCompletes, TaskResult.NeverCompletes, TaskResult.Success })]
    [InlineData(new[] { TaskResult.NeverCompletes, TaskResult.NeverCompletes, TaskResult.NeverCompletes, TaskResult.Success, TaskResult.NeverCompletes })]
    [InlineData(new[] { TaskResult.NeverCompletes, TaskResult.Success, TaskResult.NeverCompletes, TaskResult.Success, TaskResult.NeverCompletes })]
    public static async Task WhenAnySuccessful_Should_Return_A_Successful_Task(TaskResult[] taskResults)
    {
        // true = success, false = failure, null = never completes

        TaskCompletionSource<string>[] taskCompletionSources = taskResults
            .Select(p => new TaskCompletionSource<string>())
            .ToArray();

        // Just in case, don't let this test run forever (use generous timeout for slow CI)
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var completeTask = TaskHelpers.WhenAnySuccessful(taskCompletionSources.Select(p => p.Task), cts.Token);

        for (var i=0; i< taskResults.Length; i++)
        {
            var taskResult = taskResults[i];
            if (taskResult == TaskResult.Success)
            {
                taskCompletionSources[i].SetResult($"{i}");
            }
            else if (taskResult == TaskResult.Failure)
            {
                taskCompletionSources[i].SetException(new Exception("intentional failure " + i));
            }
        }

        var firstSuccessful = await completeTask
#if NET6_0_OR_GREATER
            .WaitAsync(cts.Token); // Extra safeguard against a stuck test
#else
            ;
#endif

        var index = int.Parse(firstSuccessful);

        Assert.Equal(TaskResult.Success, taskResults[index]);
    }

    [Fact]
    public async Task WhenAnySuccessful_Should_Finish_Despite_Races()
    {
        long sentinel = 0;
        // 100 is plenty: WhenAnySuccessful queues one pool continuation per task, so this is still far
        // more continuations than pool workers racing the successful one. It was 10,000, which put that
        // many items on the pool's shared queue and delayed other tests for 1-2.5 s on the CI runners.
        // A mutant that gives up at the first failure is caught at 10,000, 100 and even 10 tasks.
        var faultyTasks = Enumerable.Range(0, 100)
            .Select<int, Task<string>>(async i =>
            {
                // using var foo = new ThrowsAfterDispose();
                await Task.Delay(0);
                SpinWait.SpinUntil(() => Interlocked.Read(ref sentinel) > 0, 500);
                if (i % 3 == 0)
                {
                    await Task.Delay(1);
                }
                throw new Exception("intentional failure " + i);
            });

        var successfulTask = Task.Run(() =>
        {
            SpinWait.SpinUntil(() => false, 50);
            Interlocked.Increment(ref sentinel);
            Thread.Sleep(1);
            return "success";
        });

        var allTasks = faultyTasks.Concat(new[] { successfulTask });
        var result = await TaskHelpers.WhenAnySuccessful(allTasks, CancellationToken.None);
        Assert.Equal("success", result);
    }

    // The tests below drive each replica with a TaskCompletionSource that runs its continuations
    // inline, so WhenAnySuccessful has settled by the time SetResult, SetException or Cancel returns.
    // Each one asserts the state straight away and never waits.

    [Fact]
    public async Task WhenAnySuccessful_AllCanceled_ShouldThrowCancellationException()
    {
        var replica1 = new TaskCompletionSource<int>();
        var replica2 = new TaskCompletionSource<int>();
        var whenAnySuccessful = TaskHelpers.WhenAnySuccessful(new[] { replica1.Task, replica2.Task }, CancellationToken.None);

        replica1.SetCanceled();
        Assert.False(whenAnySuccessful.IsCompleted);
        replica2.SetCanceled();

        Assert.True(whenAnySuccessful.IsCanceled);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => whenAnySuccessful);
    }

    [Fact]
    public async Task WhenAnySuccessful_AllFailed_ShouldThrowAggregate()
    {
        var replica1 = new TaskCompletionSource<int>();
        var replica2 = new TaskCompletionSource<int>();
        var whenAnySuccessful = TaskHelpers.WhenAnySuccessful(new[] { replica1.Task, replica2.Task }, CancellationToken.None);

        replica1.SetException(new Exception("first"));
        Assert.False(whenAnySuccessful.IsCompleted);
        replica2.SetException(new Exception("second"));

        Assert.True(whenAnySuccessful.IsFaulted);
        var aggregate = await Assert.ThrowsAsync<AggregateException>(() => whenAnySuccessful);
        Assert.Equal(new[] { "first", "second" }, aggregate.InnerExceptions.Select(e => e.Message).OrderBy(m => m));
    }

    [Fact]
    public async Task WhenAnySuccessful_CanceledBeforeReplicasFail_ShouldBeCanceled()
    {
        var replica1 = new TaskCompletionSource<int>();
        var replica2 = new TaskCompletionSource<int>();
        using var cts = new CancellationTokenSource();
        var whenAnySuccessful = TaskHelpers.WhenAnySuccessful(new[] { replica1.Task, replica2.Task }, cts.Token);

        cts.Cancel();
        Assert.True(whenAnySuccessful.IsCanceled);

        replica1.SetCanceled();
        replica2.SetException(new Exception("replica read failed"));

        Assert.True(whenAnySuccessful.IsCanceled);
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => whenAnySuccessful);
        Assert.Equal(cts.Token, exception.CancellationToken);
    }

    [Fact]
    public void WhenAnySuccessful_CanceledBeforeReplicaSucceeds_ShouldBeCanceled()
    {
        var replica1 = new TaskCompletionSource<int>();
        var replica2 = new TaskCompletionSource<int>();
        using var cts = new CancellationTokenSource();
        var whenAnySuccessful = TaskHelpers.WhenAnySuccessful(new[] { replica1.Task, replica2.Task }, cts.Token);

        cts.Cancel();
        replica1.SetResult(42);
        replica2.SetCanceled();

        Assert.True(whenAnySuccessful.IsCanceled);
    }

    [Fact]
    public void WhenAnySuccessful_AlreadyCanceledToken_ShouldBeCanceled()
    {
        var replica = new TaskCompletionSource<int>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var whenAnySuccessful = TaskHelpers.WhenAnySuccessful(new[] { replica.Task }, cts.Token);

        Assert.True(whenAnySuccessful.IsCanceled);
    }

    [Fact]
    public async Task WhenAnySuccessful_CanceledAfterReplicaSucceeds_ShouldReturnResult()
    {
        var replica1 = new TaskCompletionSource<int>();
        var replica2 = new TaskCompletionSource<int>();
        using var cts = new CancellationTokenSource();
        var whenAnySuccessful = TaskHelpers.WhenAnySuccessful(new[] { replica1.Task, replica2.Task }, cts.Token);

        replica1.SetResult(7);
        cts.Cancel();
        replica2.SetCanceled();

        Assert.Equal(TaskStatus.RanToCompletion, whenAnySuccessful.Status);
        Assert.Equal(7, await whenAnySuccessful);
    }

    [Fact]
    public async Task InCompletionOrder_Returns_Tasks_In_The_Order_They_Finish()
    {
        var sources = Enumerable.Range(0, 3).Select(_ => new TaskCompletionSource<int>()).ToArray();

        var ordered = TaskHelpers.InCompletionOrder(sources.Select(s => s.Task).ToList());

        Assert.All(ordered, task => Assert.False(task.IsCompleted));

        sources[2].SetResult(2);
        Assert.Equal(2, await await ordered[0]);
        Assert.False(ordered[1].IsCompleted);

        sources[0].SetResult(0);
        Assert.Equal(0, await await ordered[1]);

        sources[1].SetResult(1);
        Assert.Equal(1, await await ordered[2]);
    }

    [Fact]
    public async Task InCompletionOrder_Hands_Back_A_Faulted_Task_As_Its_Result()
    {
        var failed = new TaskCompletionSource<int>();
        var succeeded = new TaskCompletionSource<int>();

        var ordered = TaskHelpers.InCompletionOrder(new List<Task<int>> { succeeded.Task, failed.Task });

        failed.SetException(new InvalidOperationException("first to finish"));
        var first = await ordered[0];
        Assert.Same(failed.Task, first);
        await Assert.ThrowsAsync<InvalidOperationException>(() => first);

        succeeded.SetResult(1);
        Assert.Equal(1, await await ordered[1]);
    }

    [Fact]
    public void InCompletionOrder_Of_No_Tasks_Is_Empty()
    {
        Assert.Empty(TaskHelpers.InCompletionOrder(new List<Task<int>>()));
    }
}
