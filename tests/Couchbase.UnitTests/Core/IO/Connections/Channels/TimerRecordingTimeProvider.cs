using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;

namespace Couchbase.UnitTests.Core.IO.Connections.Channels
{
    /// <summary>
    /// A fake clock that reports the due time of each timer the code under test creates. A test waits for the
    /// next timer before it advances the clock, so an advance is never lost.
    /// </summary>
    internal sealed class TimerRecordingTimeProvider : FakeTimeProvider
    {
        private readonly Channel<TimeSpan> _timers = Channel.CreateUnbounded<TimeSpan>();
        private int _timersCreated;

        public int TimersCreated => Volatile.Read(ref _timersCreated);

        public override ITimer CreateTimer(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = base.CreateTimer(callback, state, dueTime, period);
            Interlocked.Increment(ref _timersCreated);
            _timers.Writer.TryWrite(dueTime);
            return timer;
        }

        public async Task<TimeSpan> NextTimerAsync(CancellationToken cancellationToken) =>
            await _timers.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
    }
}
