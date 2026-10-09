#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Couchbase.UnitTests.Helpers;

/// <summary>
/// Runs a concurrency test's workers on dedicated threads instead of thread-pool workers.
/// <para>
/// A worker that blocks (a <see cref="Barrier"/>, <see cref="Thread.Sleep(int)"/>) or spins holds a
/// pool thread that every other test running in parallel needs. The pool starts with one worker per
/// CPU (4 on the CI runners), and injects more for a blocked <see cref="Barrier"/> only at about one
/// per second, so a barrier wider than the free workers stalls the whole test process for seconds.
/// A dedicated thread gives the test the same real concurrency and leaves the pool alone.
/// </para>
/// </summary>
internal static class DedicatedThreads
{
    /// <summary>
    /// Runs <paramref name="action"/> on a new dedicated thread.
    /// </summary>
    public static Task Run(Action action) =>
        Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    /// <summary>
    /// Runs <paramref name="action"/> on <paramref name="count"/> dedicated threads, passing each its
    /// index, and completes when all of them have.
    /// </summary>
    public static Task RunAll(int count, Action<int> action) =>
        Task.WhenAll(Enumerable.Range(0, count).Select(i => Run(() => action(i))));
}
