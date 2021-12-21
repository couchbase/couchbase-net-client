using System;
using System.Diagnostics;

#nullable enable

namespace Couchbase.Utils
{
    /// <summary>
    /// A lightweight stopwatch implementation.
    /// </summary>
    /// <remarks>
    /// This implementation doesn't support stopping, and has a poor resolution compared to
    /// <see cref="Stopwatch"/> (approximately 10-16 milliseconds). However, on .NET Core 3.1 and later
    /// it avoids heap allocations in cases where higher resolution is not required. For older frameworks
    /// we fallback to a <see cref="Stopwatch"/> since <c>Environment.TickCount64</c> is not available
    /// and <see cref="Environment.TickCount"/> has issues wrapping to negative numbers.
    /// </remarks>
    internal struct LightweightStopwatch
    {
#if NETCOREAPP3_1_OR_GREATER
        private long _startTicks;
#else
        private Stopwatch? _stopwatch;
#endif

        /// <summary>
        /// Creates and starts a new <see cref="LightweightStopwatch"/>.
        /// </summary>
        /// <returns>The <see cref="LightweightStopwatch"/>.</returns>
        public static LightweightStopwatch StartNew() =>
            new()
            {
#if NETCOREAPP3_1_OR_GREATER
                _startTicks = Environment.TickCount64
#else
                _stopwatch = Stopwatch.StartNew()
#endif
            };

        /// <summary>
        /// Elapsed milliseconds since the stopwatch was started.
        /// </summary>
        /// <remarks>
        /// Resolution is 10-16 milliseconds.
        /// </remarks>
        public readonly long ElapsedMilliseconds =>
#if NETCOREAPP3_1_OR_GREATER
            Environment.TickCount64 - _startTicks;
#else
            _stopwatch?.ElapsedMilliseconds ?? 0;
#endif

        /// <summary>
        /// Elapsed time since the stopwatch was started.
        /// </summary>
        /// <remarks>
        /// Resolution is 10-16 milliseconds.
        /// </remarks>
        public readonly TimeSpan Elapsed =>
#if NETCOREAPP3_1_OR_GREATER
            TimeSpan.FromMilliseconds(ElapsedMilliseconds);
#else
            _stopwatch?.Elapsed ?? TimeSpan.Zero;
#endif

        /// <summary>
        /// Restart the stopwatch from zero.
        /// </summary>
        public void Restart()
        {
#if NETCOREAPP3_1_OR_GREATER
            _startTicks = Environment.TickCount64;
#else
            // It is a corner case that shouldn't happen where _stopwatch is null, it should be created
            // by StartNew(). Just in case we create this structure using `= default` or similar, we'll
            // make the stopwatch when we restart.
            (_stopwatch ??= new Stopwatch()).Restart();
#endif
        }
    }
}
