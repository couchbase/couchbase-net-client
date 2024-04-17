using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Couchbase.Utils;

public static class TaskHelpers
{
    [Obsolete("This method was not intended to be public.  It will go away in a future version.")]
    public static Task<T> WhenAnySuccessful<T>(IEnumerable<Task<T>> tasks) =>
        WhenAnySuccessful(tasks, default(CancellationToken));
    internal static Task<T> WhenAnySuccessful<T>(IEnumerable<Task<T>> tasks, System.Threading.CancellationToken cancellationToken)
    {
        var taskList = tasks.ToArray();
        var cancelRemainingContinuations = new CancellationTokenSource();
        var tcs = new TaskCompletionSource<T>();
        var tasksCount = taskList.Length;

        if (tasksCount == 0)
        {
            tcs.SetException(new ArgumentException(nameof(taskList) + " cannot be empty."));
            return tcs.Task;
        }
        var unsuccessfulTasks = new ConcurrentBag<Task<T>>();

        // A local function to avoid repeated delegate creation for each task.
        // Local instead of static, because it captures local variables.
        void HandleCompletion(Task<T> t)
        {
            if (t.IsFaulted || t.IsCanceled)
            {
                unsuccessfulTasks.Add(t);

                // If all tasks failed/canceled, set aggregate as exception
                if (unsuccessfulTasks.Count == tasksCount)
                {
                    var cancelledCount = unsuccessfulTasks.Count(ct => ct.IsCanceled);
                    if (cancelledCount >= tasksCount)
                    {
                        cancelRemainingContinuations.Cancel();
                        tcs.TrySetCanceled();
                    }
                    else
                    {
                        var innerExceptions = unsuccessfulTasks.Where(ct => ct.Exception is not null).SelectMany(ct => ct.Exception.InnerExceptions);
                        tcs.TrySetException(new AggregateException(innerExceptions));
                    }
                }
            }
            else //If Completed and not Faulted or Canceled, can only be successful
            {
                if (tcs.TrySetResult(t.Result))
                {
                    cancelRemainingContinuations.Cancel();
                }
            }
        }

        // a local function to handle edge cases only after all tasks have ended.
        void Failsafe(Task whenAll)
        {
            // just in case all tasks have finished, but none successfully TrySetResult/Cancelled/Exception,
            // make sure the calling code will terminate one way or another
            if (tcs.Task.Status == TaskStatus.RanToCompletion)
            {
                return;
            }

            foreach (var rt in taskList)
            {
                if (rt.Status == TaskStatus.RanToCompletion && tcs.TrySetResult(rt.Result))
                {
                    return;
                }
            }

            foreach (var rt in taskList)
            {
                if (rt.IsFaulted && tcs.TrySetException(rt.Exception!))
                {
                    return;
                }
            }

            // only remaining possibility is cancelled.
            if (!tcs.TrySetCanceled())
            {
                // but if that failed, who knows what happened.
                tcs.SetException(new InvalidOperationException("WhenAnySuccessful reached an impossible state"));
            }
        }

        Action<Task<T>> handleCompletionAction = HandleCompletion;
        foreach (var task in taskList)
        {
            task.ContinueWith(
                continuationAction: handleCompletionAction,
                continuationOptions: TaskContinuationOptions.None,
                cancellationToken: cancellationToken,
                scheduler: TaskScheduler.Default);
        }

        // handle edge cases where no result or exception has been set
        Action<Task> failSafeAction = Failsafe;
        var allFinished = Task.WhenAll(taskList);
        _ = allFinished.ContinueWith(
            continuationAction: failSafeAction,
            continuationOptions: TaskContinuationOptions.None,
            cancellationToken: cancellationToken,
            scheduler: TaskScheduler.Default);

        return tcs.Task;
    }
}
