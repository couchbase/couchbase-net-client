using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Couchbase.Utils;

public static class TaskHelpers
{
    public static Task<T> WhenAnySuccessful<T>(IEnumerable<Task<T>> taskList)
    {
        var tcs = new TaskCompletionSource<T>();
        var tasksCount = taskList.Count();

        if (tasksCount == 0)
        {
            tcs.SetException(new ArgumentException(nameof(taskList) + " cannot be empty."));
            return tcs.Task;
        }
        var completedTasks = new List<Task<T>>(tasksCount);

        foreach (var task in taskList)
        {
            task.ContinueWith(t =>
            {
                if (t.IsFaulted || t.IsCanceled)
                {
                    completedTasks.Add(t);

                    // If the last task failed/canceled, set aggregate as exception
                    if (completedTasks.Count == tasksCount)
                    {
                        tcs.SetException(new AggregateException(completedTasks.SelectMany(completedTask => completedTask.Exception?.InnerExceptions)));
                    }
                }
                else //If Completed and not Faulted or Canceled, can only be successful
                {
                    if (tcs.TrySetResult(t.Result))
                    {
                        foreach (var remainingTask in taskList)
                        {
                            if (remainingTask != t)
                            {
                                remainingTask.ContinueWith(_ => tcs.TrySetCanceled());
                            }
                        }
                    }
                    else
                    {
                        throw new CouchbaseException("Could not set result for unknown reasons.");
                    }
                }
            });
        }
        return tcs.Task;
    }
}
