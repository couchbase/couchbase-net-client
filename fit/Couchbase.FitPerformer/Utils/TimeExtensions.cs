using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.FitPerformer.Utils
{
    public static class TimeExtensions
    {
        public const long NanosPerSecond = Google.Protobuf.WellKnownTypes.Duration.NanosecondsPerSecond; // 1 billion
        public const long NanosPerTick = (long)(NanosPerSecond / TimeSpan.TicksPerSecond);

        /// <summary>
        /// Convert Ticks to Nanoseconds.
        /// </summary>
        /// <param name="ticks">A time value measured in ticks</param>
        /// <returns>The equivalent Nanoseconds, only as precise as a tick.</returns>
        public static long TicksToNanos(long ticks) => ticks * NanosPerTick;

        /// <summary>
        /// Calculate the nanoseconds elapsed based on the ticks.
        /// </summary>
        /// <param name="ts">The TimeSpan to measure.</param>
        /// <returns>The number of nanoseconds represented by this TimeSpan, to the resolution of a Tick (i.e. 100ns)</returns>
        /// <remarks>If you are using this with a Stopwatch, stop the Stopwatch first.  The calculation time dwarfs the actual elapsed time at this resolution.</remarks>
        public static long CalculateNanos(this TimeSpan ts) => TicksToNanos(ts.Ticks);
    }
}
