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
        var faultyTasks = Enumerable.Range(0, 10_000)
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

    [Fact]
    public void WhenAnySuccessful_AllCanceled_ShouldThrowCancellationException()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));
        var t1 = Task.Run(async () =>
        {
            await Task.Delay(10, cts.Token);
            cts.Token.ThrowIfCancellationRequested();
            return 1;
        }, cts.Token);
        var t2 = Task.Run(async () =>
        {
            await Task.Delay(10, cts.Token);
            cts.Token.ThrowIfCancellationRequested();
            return 2;
        }, cts.Token);

        var whenAnySuccessful = TaskHelpers.WhenAnySuccessful(new List<Task<int>>() { t1, t2 }, cts.Token);
#pragma warning disable xUnit2021
        _ = Assert.ThrowsAsync<OperationCanceledException>(() => whenAnySuccessful);
#pragma warning restore xUnit2021
    }

    [Fact]
    public void WhenAnySuccessful_AllFailed_ShouldThrowAggregate()
    {
        Func<int> alwaysThrows = () => throw new Exception("foo");
        var t1 = Task.Run(alwaysThrows);
        var t2 = Task.Run(alwaysThrows);
        var whenAnySuccessful = TaskHelpers.WhenAnySuccessful(new List<Task<int>>() { t1, t2 }, CancellationToken.None);
#pragma warning disable xUnit2021
        _ = Assert.ThrowsAsync<AggregateException>(() => whenAnySuccessful);
#pragma warning restore xUnit2021
    }
}
