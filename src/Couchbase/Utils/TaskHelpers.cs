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
        // The continuations below complete tcs inline, so the caller must not resume inside them,
        // nor inside cancellationToken's Cancel().
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasksCount = taskList.Length;

        if (tasksCount == 0)
        {
            tcs.SetException(new ArgumentException(nameof(taskList) + " cannot be empty."));
            return tcs.Task;
        }
        var unsuccessfulTasks = new ConcurrentBag<Task<T>>();

        // The continuations run whatever the token does, because a continuation cancelled by its
        // token never runs and would leave tcs pending forever. The token cancels tcs directly instead.
        if (cancellationToken.CanBeCanceled)
        {
            var registration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
            _ = tcs.Task.ContinueWith(
                _ => registration.Dispose(),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

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
                        tcs.TrySetCanceled(cancellationToken);
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
                tcs.TrySetResult(t.Result);
            }
        }

        // a local function to handle edge cases only after all tasks have ended.
        void Failsafe(Task whenAll)
        {
            // whenAll holds every replica's fault. Observe it, or each fault is reported as an
            // unobserved task exception.
            _ = whenAll.Exception;

            // just in case all tasks have finished, but none successfully TrySetResult/Cancelled/Exception,
            // make sure the calling code will terminate one way or another
            if (tcs.Task.IsCompleted)
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

            // only remaining possibility is cancelled. This fails only if the token cancelled tcs
            // since the check above, which leaves it completed either way.
            tcs.TrySetCanceled(cancellationToken);
        }

        // The continuations are cheap and run inline, so a replica completing completes tcs before
        // it returns. Neither takes the caller's token; see the registration above.
        Action<Task<T>> handleCompletionAction = HandleCompletion;
        foreach (var task in taskList)
        {
            task.ContinueWith(
                continuationAction: handleCompletionAction,
                continuationOptions: TaskContinuationOptions.ExecuteSynchronously,
                cancellationToken: CancellationToken.None,
                scheduler: TaskScheduler.Default);
        }

        // handle edge cases where no result or exception has been set. Registered after the
        // per-task continuations, so it runs after them.
        Action<Task> failSafeAction = Failsafe;
        var allFinished = Task.WhenAll(taskList);
        _ = allFinished.ContinueWith(
            continuationAction: failSafeAction,
            continuationOptions: TaskContinuationOptions.ExecuteSynchronously,
            cancellationToken: CancellationToken.None,
            scheduler: TaskScheduler.Default);

        return tcs.Task;
    }

    /// <summary>
    /// Orders tasks by when they finish. The nth task returned completes when the nth of
    /// <paramref name="tasks"/> finishes, and its result is that finished task. This is
    /// Task.WhenEach, which only exists from .NET 9.
    /// </summary>
    /// <remarks>
    /// Every faulted task is marked as observed, so a caller that stops reading early does not
    /// leave unobserved exceptions behind. Awaiting a returned task still throws.
    /// </remarks>
    internal static Task<Task<T>>[] InCompletionOrder<T>(IReadOnlyList<Task<T>> tasks)
    {
        var buckets = new TaskCompletionSource<Task<T>>[tasks.Count];
        var ordered = new Task<Task<T>>[buckets.Length];
        for (var i = 0; i < buckets.Length; i++)
        {
            buckets[i] = new TaskCompletionSource<Task<T>>(TaskCreationOptions.RunContinuationsAsynchronously);
            ordered[i] = buckets[i].Task;
        }

        var next = -1;
        foreach (var task in tasks)
        {
            task.ContinueWith(completed =>
                {
                    _ = completed.Exception;
                    buckets[Interlocked.Increment(ref next)].TrySetResult(completed);
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        return ordered;
    }

    /// <summary>
    /// Disposes the result of each task when it completes successfully, including tasks that complete
    /// later. A fault is observed, so that it is not reported as an unobserved task exception.
    /// </summary>
    /// <remarks>
    /// For a stream that stops early: the caller owns only the results it was given, so the stream
    /// disposes the rest.
    /// </remarks>
    internal static void DisposeWhenCompleted<T>(IEnumerable<Task<T>> tasks) where T : IDisposable
    {
        foreach (var task in tasks)
        {
            _ = task.ContinueWith(static finished =>
                {
                    if (finished.Status == TaskStatus.RanToCompletion)
                    {
                        finished.Result.Dispose();
                    }
                    else
                    {
                        _ = finished.Exception;
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }
}
