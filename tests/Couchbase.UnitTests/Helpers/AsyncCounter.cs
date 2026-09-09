#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Couchbase.UnitTests.Helpers;

/// <summary>
/// Counts something the code under test does — a call it makes, a callback it invokes, a lap of a
/// loop — and lets a test await the nth occurrence with no timeout and no polling.
/// <para>
/// Awaiting an occurrence which has already happened returns immediately, so a test can never miss
/// one by arriving late, and awaiting one which never happens hangs the test rather than failing an
/// assertion on a slow machine: a hang is unambiguous, a wall-clock deadline is not. See
/// [NCBC-4293].
/// </para>
/// </summary>
internal sealed class AsyncCounter
{
    private readonly object _lock = new();
    private readonly List<(long Occurrence, TaskCompletionSource<bool> Reached)> _waiters = new();
    private long _count;

    /// <summary>
    /// How many occurrences have been counted so far.
    /// </summary>
    public long Count
    {
        get
        {
            lock (_lock)
            {
                return _count;
            }
        }
    }

    /// <summary>
    /// Counts one occurrence, releasing any test waiting for it.
    /// </summary>
    public void Increment()
    {
        List<TaskCompletionSource<bool>>? reached = null;

        lock (_lock)
        {
            _count++;

            for (var i = _waiters.Count - 1; i >= 0; i--)
            {
                var (occurrence, waiter) = _waiters[i];
                if (occurrence <= _count)
                {
                    (reached ??= new List<TaskCompletionSource<bool>>()).Add(waiter);
                    _waiters.RemoveAt(i);
                }
            }
        }

        // Completed outside the lock, so a waiter's continuation is free to call straight back in.
        if (reached is not null)
        {
            foreach (var waiter in reached)
            {
                waiter.TrySetResult(true);
            }
        }
    }

    /// <summary>
    /// Completes once <paramref name="occurrence"/> occurrences have been counted, whether that has
    /// already happened or is still to come.
    /// </summary>
    public Task WaitForAsync(long occurrence)
    {
        lock (_lock)
        {
            if (_count >= occurrence)
            {
                return Task.CompletedTask;
            }

            var reached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.Add((occurrence, reached));
            return reached.Task;
        }
    }
}
