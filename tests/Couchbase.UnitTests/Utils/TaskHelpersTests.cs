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
    [Theory]
    [InlineData("TaskFive", new[] {true, true, true, true, false})]
    [InlineData("TaskFour", new[] {true, true, true, false, true})]
    [InlineData("TaskTwo", new[] {true, false, true, false, true})]
    public static async Task WhenAnySuccessful_Should_Return_First_Successful_Task(string expectedId, bool[] throws)
    {
        var taskOne = WaitOrThrow("TaskOne", 1, throws[0]);
        var taskTwo = WaitOrThrow("TaskTwo", 2, throws[1]);
        var taskThree = WaitOrThrow("TaskThree", 3, throws[2]);
        var taskFour = WaitOrThrow("TaskFour", 4, throws[3]);
        var taskFive = WaitOrThrow("TaskFive", 5, throws[4]);

        var taskList = new[] { taskOne, taskTwo, taskThree , taskFour, taskFive};

        var firstSuccessful = await TaskHelpers.WhenAnySuccessful(taskList, CancellationToken.None);

        Assert.Equal(expectedId, firstSuccessful);
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

    private static async Task<string> WaitOrThrow(string id, int seconds, bool throws)
    {
        await Task.Delay(TimeSpan.FromSeconds(seconds));
        if (throws)
        {
            throw new Exception($"Task {id} threw.");
        }
        return id;
    }
}
