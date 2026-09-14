#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Time.Testing;
using System.Threading.Tasks;

namespace Couchbase.UnitTests.Helpers;

/// <summary>
/// A <see cref="TimeProvider"/> for testing code which paces itself with a sleep — a background
/// polling loop, a retry backoff, an emit interval. It is a <see cref="FakeTimeProvider"/>, so no
/// test waits on the real clock, and it additionally lets a test wait until the code under test has
/// actually asked to sleep. That closes the race which otherwise loses an <see cref="Advance"/> made
/// before the sleep was registered, and hangs the test.
/// <para>
/// Await <see cref="WaitForSleepAsync"/>, then <see cref="Advance"/>, to step the code under test one
/// sleep at a time. Successive calls for the same duration wait for successive sleeps, so a test
/// reads as the sequence of laps it expects. Nothing has a deadline: code which stops sleeping when
/// it should not makes the test hang — reported as a hang, and diagnosable — rather than failing a
/// wall-clock assertion on a busy CI runner (NCBC-4293).
/// </para>
/// <para>
/// Reaching a sleep is also the signal that a lap has been processed to completion, which is what
/// lets assertions run against state the code under test can no longer be mutating.
/// </para>
/// <para>
/// Only step a loop the test itself starts. Stepping requires the code under test to run between the
/// steps, on some other thread, so a class which starts background loops in its constructor makes the
/// test depend on the thread pool scheduling them — which deadlocks outright when the pool has no
/// thread to give. That is not hypothetical: it hung the net48 CI leg for the full blame-hang timeout
/// while net8.0 and net10.0 passed, and reproduces on any framework by forcing the pool to a single
/// worker. Drive such a class through its own steps instead, as OrphanedResponseTests does.
/// </para>
/// </summary>
internal sealed class SteppableClock : TimeProvider
{
    private readonly FakeTimeProvider _clock = new();
    private readonly object _lock = new();
    private readonly Dictionary<TimeSpan, AsyncCounter> _sleeps = new();
    private readonly Dictionary<TimeSpan, long> _awaited = new();

    public override DateTimeOffset GetUtcNow() => _clock.GetUtcNow();
    public override long GetTimestamp() => _clock.GetTimestamp();
    public override long TimestampFrequency => _clock.TimestampFrequency;
    public override TimeZoneInfo LocalTimeZone => _clock.LocalTimeZone;

    /// <summary>
    /// Every sleep on this clock arrives here, whether it was taken through <c>Delay</c> or by
    /// creating a timer directly.
    /// </summary>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        // Created before it is counted, so a test woken by the count cannot advance the clock before
        // the timer it means to fire exists.
        var timer = _clock.CreateTimer(callback, state, dueTime, period);

        AsyncCounter sleeps;
        lock (_lock)
        {
            sleeps = SleepsOf(dueTime);
        }

        // Counted outside the lock: this is what releases a waiting test.
        sleeps.Increment();
        return timer;
    }

    /// <summary>
    /// Moves the clock on, ending any sleep which has now elapsed.
    /// </summary>
    public void Advance(TimeSpan duration) => _clock.Advance(duration);

    /// <summary>
    /// Completes once the code under test is sleeping for <paramref name="duration"/>. The nth call
    /// for a given duration waits for the nth such sleep, whether it has happened yet or not.
    /// </summary>
    public Task WaitForSleepAsync(TimeSpan duration)
    {
        AsyncCounter sleeps;
        long awaited;

        lock (_lock)
        {
            sleeps = SleepsOf(duration);
            _awaited.TryGetValue(duration, out awaited);
            _awaited[duration] = ++awaited;
        }

        return sleeps.WaitForAsync(awaited);
    }

    /// <summary>
    /// The sleeps taken for one duration. Callers hold <see cref="_lock"/>.
    /// </summary>
    private AsyncCounter SleepsOf(TimeSpan duration)
    {
        if (!_sleeps.TryGetValue(duration, out var sleeps))
        {
            sleeps = new AsyncCounter();
            _sleeps.Add(duration, sleeps);
        }

        return sleeps;
    }
}
