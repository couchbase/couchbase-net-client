using System;
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
    public static async void WhenAnySuccessful_Should_Return_First_Successful_Task(string expectedId, bool[] throws)
    {
        var taskOne = WaitOrThrow("TaskOne", 1, throws[0]);
        var taskTwo = WaitOrThrow("TaskTwo", 2, throws[1]);
        var taskThree = WaitOrThrow("TaskThree", 3, throws[2]);
        var taskFour = WaitOrThrow("TaskFour", 4, throws[3]);
        var taskFive = WaitOrThrow("TaskFive", 5, throws[4]);

        var taskList = new[] { taskOne, taskTwo, taskThree , taskFour, taskFive};

        var firstSuccessful = await TaskHelpers.WhenAnySuccessful(taskList).ConfigureAwait(false);

        Assert.Equal(expectedId, firstSuccessful);
    }

    private static async Task<string> WaitOrThrow(string id, int seconds, bool throws)
    {
        await Task.Delay(TimeSpan.FromSeconds(seconds)).ConfigureAwait(false);
        if (throws)
        {
            throw new Exception($"Task {id} threw.");
        }
        return id;
    }
}
