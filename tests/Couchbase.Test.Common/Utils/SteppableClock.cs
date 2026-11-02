#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Couchbase.Test.Common.Utils;

/// <summary>
/// A <see cref="TimeProvider"/> for testing code which paces itself with a sleep, such as a background
/// polling loop, a retry backoff or an emit interval. Time moves only when the test calls <see cref="Advance"/>,
/// so no test waits on the real clock. A test can also wait until the code under test has asked to sleep.
/// That closes the race which otherwise loses an <see cref="Advance"/> made before the sleep was registered,
/// and hangs the test.
/// <para>
/// Await <see cref="WaitForSleepAsync"/>, then <see cref="Advance"/>, to step the code under test one
/// sleep at a time. Successive calls for the same duration wait for successive sleeps, so a test
/// reads as the sequence of laps it expects. Nothing has a deadline. Code which stops sleeping when
/// it should not makes the test hang, which is reported as a hang and is diagnosable, rather than failing a
/// wall-clock assertion on a busy CI runner (NCBC-4293).
/// </para>
/// <para>
/// Reaching a sleep is also the signal that a lap has been processed to completion, which is what
/// lets assertions run against state the code under test can no longer be mutating.
/// </para>
/// <para>
/// Only step a loop the test itself starts. Stepping requires the code under test to run between the
/// steps, on some other thread, so a class which starts background loops in its constructor makes the
/// test depend on the thread pool scheduling them. That deadlocks outright when the pool has no
/// thread to give. It is not hypothetical. It hung the net48 CI leg for the full blame-hang timeout
/// while net8.0 and net10.0 passed, and reproduces on any framework by forcing the pool to a single
/// worker. Drive such a class through its own steps instead, as OrphanedResponseTests does.
/// </para>
/// <para>
/// Its timing rules match FakeTimeProvider. Time starts at 2000-01-01 UTC. A timer due now fires on the thread that
/// schedules it. <see cref="Advance"/> sets the new time first, then fires every timer due by then, earliest first,
/// on the calling thread. <see cref="PendingTimers"/> lists the timers that are still to fire.
/// </para>
/// </summary>
internal sealed class SteppableClock : TimeProvider
{
    private static readonly DateTimeOffset Start = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly object _lock = new();
    private readonly Dictionary<TimeSpan, AsyncCounter> _sleeps = new();
    private readonly Dictionary<TimeSpan, long> _awaited = new();
    private readonly List<ClockTimer> _pending = new();
    private long _nowTicks = Start.UtcTicks;
    private long _lastSequence;
    private long _timersCreated;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_lock)
        {
            return new DateTimeOffset(_nowTicks, TimeSpan.Zero);
        }
    }

    public override long GetTimestamp()
    {
        lock (_lock)
        {
            return _nowTicks;
        }
    }

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    /// <summary>
    /// How many timers were created on this clock, including the ones that already fired or were disposed.
    /// </summary>
    public long TimersCreated
    {
        get
        {
            lock (_lock)
            {
                return _timersCreated;
            }
        }
    }

    /// <summary>
    /// The due time of the earliest pending timer, or null when no timer is pending.
    /// </summary>
    public DateTimeOffset? NextDueTime
    {
        get
        {
            lock (_lock)
            {
                return Earliest(long.MaxValue) is { } timer ? new DateTimeOffset(timer.DueTicks, TimeSpan.Zero) : null;
            }
        }
    }

    /// <summary>
    /// The timers that fire when the clock moves far enough, earliest first. A timer is pending from the time it is
    /// scheduled until it fires (a one shot timer), is changed to an infinite due time, or is disposed.
    /// </summary>
    public IReadOnlyList<PendingTimer> PendingTimers
    {
        get
        {
            lock (_lock)
            {
                return _pending
                    .OrderBy(t => t.DueTicks)
                    .ThenBy(t => t.Sequence)
                    .Select(t => new PendingTimer(new DateTimeOffset(t.DueTicks, TimeSpan.Zero), TimeSpan.FromTicks(t.PeriodTicks)))
                    .ToList();
            }
        }
    }

    /// <summary>
    /// Every sleep on this clock arrives here, whether it was taken through <c>Delay</c> or by
    /// creating a timer directly.
    /// </summary>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        if (callback is null) throw new ArgumentNullException(nameof(callback));

        var timer = new ClockTimer(this, callback, state);
        lock (_lock)
        {
            _timersCreated++;
        }

        // Scheduled before it is counted, so a test woken by the count cannot advance the clock before
        // the timer it means to fire exists.
        timer.Change(dueTime, period);

        AsyncCounter sleeps;
        lock (_lock)
        {
            sleeps = SleepsOf(dueTime);
        }

        // Counted outside the lock, because this releases a waiting test.
        sleeps.Increment();
        return timer;
    }

    /// <summary>
    /// Moves the clock on, ending any sleep which has now elapsed.
    /// </summary>
    public void Advance(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration), duration, "The clock cannot go back.");

        lock (_lock)
        {
            _nowTicks += duration.Ticks;
        }

        FireDueTimers();
    }

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

    private bool Schedule(ClockTimer timer, TimeSpan dueTime, TimeSpan period)
    {
        if (dueTime < TimeSpan.Zero && dueTime != Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(dueTime), dueTime, "The due time must be zero or more, or infinite.");
        }
        if (period < TimeSpan.Zero && period != Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(period), period, "The period must be zero or more, or infinite.");
        }

        lock (_lock)
        {
            if (timer.Disposed) return false;

            _pending.Remove(timer);
            if (dueTime == Timeout.InfiniteTimeSpan) return true;

            timer.DueTicks = _nowTicks + dueTime.Ticks;
            // A zero or infinite period means the timer fires once, as with System.Threading.Timer.
            timer.PeriodTicks = period == Timeout.InfiniteTimeSpan ? 0 : period.Ticks;
            timer.Sequence = ++_lastSequence;
            _pending.Add(timer);
        }

        if (dueTime == TimeSpan.Zero)
        {
            FireDueTimers();
        }
        return true;
    }

    private void Cancel(ClockTimer timer)
    {
        lock (_lock)
        {
            timer.Disposed = true;
            _pending.Remove(timer);
        }
    }

    // Callbacks run outside the lock, so a callback can create, change or dispose timers.
    private void FireDueTimers()
    {
        while (true)
        {
            ClockTimer? due;
            lock (_lock)
            {
                due = Earliest(_nowTicks);
                if (due is null) return;

                if (due.PeriodTicks > 0)
                {
                    due.DueTicks += due.PeriodTicks;
                    due.Sequence = ++_lastSequence;
                }
                else
                {
                    _pending.Remove(due);
                }
            }

            due.Fire();
        }
    }

    /// <summary>
    /// The pending timer due first, at or before <paramref name="ticks"/>. Callers hold <see cref="_lock"/>.
    /// </summary>
    private ClockTimer? Earliest(long ticks)
    {
        ClockTimer? earliest = null;
        foreach (var timer in _pending)
        {
            if (timer.DueTicks > ticks) continue;
            if (earliest is null || timer.DueTicks < earliest.DueTicks ||
                (timer.DueTicks == earliest.DueTicks && timer.Sequence < earliest.Sequence))
            {
                earliest = timer;
            }
        }

        return earliest;
    }

    /// <summary>
    /// A timer that has not fired yet.
    /// </summary>
    internal readonly struct PendingTimer(DateTimeOffset dueTime, TimeSpan period)
    {
        public DateTimeOffset DueTime { get; } = dueTime;

        /// <summary>
        /// Zero for a one shot timer.
        /// </summary>
        public TimeSpan Period { get; } = period;
    }

    /// <summary>
    /// The schedule fields are guarded by the lock of the clock.
    /// </summary>
    private sealed class ClockTimer(SteppableClock clock, TimerCallback callback, object? state) : ITimer
    {
        public long DueTicks { get; set; }

        public long PeriodTicks { get; set; }

        /// <summary>
        /// Orders timers that are due at the same time.
        /// </summary>
        public long Sequence { get; set; }

        public bool Disposed { get; set; }

        public bool Change(TimeSpan dueTime, TimeSpan period) => clock.Schedule(this, dueTime, period);

        public void Fire() => callback(state);

        public void Dispose() => clock.Cancel(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return default;
        }
    }
}
