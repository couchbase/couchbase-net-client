using System;
using System.Text.RegularExpressions;

namespace Couchbase.Utils
{
    public static class TimeSpanExtensions
    {
        private const long NanosecondsPerSecond = 1_000_000_000;
        private const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
        private static readonly TimeSpan RelativeTtlThreshold = TimeSpan.FromDays(30);
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1);

        /// <summary>
        /// Converts a <see cref="TimeSpan"/> to an <see cref="uint"/> in Microseconds.
        /// </summary>
        /// <remarks>This will overflow at 1hour 11min 34s, which shouldn't be encountered
        /// in most cases, but could throw an exception in edge cases like wrapping a span
        /// around a background operation or leaving the code on a breakpoint and going to
        /// lunch.</remarks>
        /// <param name="duration">The <see cref="TimeSpan"/> duration to convert to microseconds.</param>
        /// <returns>The microsecond equivalent of the passed in duration.</returns>
        internal static uint ToMicroseconds(this TimeSpan duration) =>
            (uint)(duration.Ticks / TicksPerMicrosecond);

        /// <summary>
        /// Converts a <see cref="TimeSpan" /> into an uint correctly representing a Time-To-Live,
        /// that is expressed in seconds.
        /// Durations strictly bigger than 30 days are converted to a unix-syle timestamp (seconds since the Epoch),
        /// as described in the couchbase TTL documentation.
        /// </summary>
        /// <returns>The TTL, expressed as a suitable uint.</returns>
        public static uint ToTtl(this TimeSpan duration)
        {
            if (duration <= RelativeTtlThreshold)
            {
                var totalSeconds = duration.TotalSeconds;

                //round up so ttl is not infinite (0)
                return totalSeconds >= 1
                    ? (uint) totalSeconds
                    : totalSeconds > 0
                        ? 1u
                        : 0u;
            }
            else
            {
                var dateExpiry = DateTime.UtcNow + duration;
                var unixTimeStamp = (uint) dateExpiry.Subtract(UnixEpoch).TotalSeconds;
                return unixTimeStamp;
            }
        }

        /// <summary>
        /// Converts a relative <see cref="TimeSpan"/> duration to an absolute <see cref="DateTimeOffset"/>
        /// representing when the TTL should expire. Used by transactions to capture the expiry time
        /// at staging, so the TTL clock starts from staging rather than unstaging.
        /// </summary>
        /// <param name="duration">The relative TTL duration.</param>
        /// <returns>An absolute <see cref="DateTimeOffset"/> representing the expiry time.</returns>
        public static DateTimeOffset ToEpochTtl(this TimeSpan duration)
        {
            return DateTimeOffset.UtcNow + duration;
        }

        /// <summary>
        /// Converts an absolute expiry <see cref="DateTimeOffset"/> back to the remaining
        /// <see cref="TimeSpan"/> relative to now. Returns a minimum of 1 second to avoid
        /// passing 0 to the server, which would mean "never expire".
        /// </summary>
        /// <param name="absoluteExpiry">The absolute expiry time.</param>
        /// <returns>The remaining TTL as a <see cref="TimeSpan"/>, minimum 1 second.</returns>
        public static TimeSpan RemainingTtl(this DateTimeOffset absoluteExpiry)
        {
            var remaining = absoluteExpiry - DateTimeOffset.UtcNow;
            return remaining > TimeSpan.FromSeconds(1)
                ? remaining
                : TimeSpan.FromSeconds(1);
        }

        /// <summary>
        /// Converts a duration expressed as milliseconds to a unix-based TTL.
        /// </summary>
        /// <param name="duration">Milliseconds to use as TTL.</param>
        /// <returns>The TTL, expressed as a unix-based TTL in milliseconds.</returns>
        public static uint ToTtl(this uint duration)
        {
            return ToTtl(TimeSpan.FromMilliseconds(duration));
        }

        /// <summary>
        /// Retrieves the number of seconds expressed in a <see cref="TimeSpan"/> as an <see cref="uint"/>.
        /// </summary>
        /// <param name="timeSpan">The timespan.</param>
        /// <returns>An <see cref="uint"/> that is the total number of seconds in the <see cref="TimeSpan"/>.</returns>
        public static uint GetSeconds(this TimeSpan timeSpan)
        {
            return (uint) timeSpan.TotalSeconds;
        }

        private static readonly Regex DurationValueSuffixRegex = new Regex(@"([\d]+)\s?([^\d]+)?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private const string MicrosSuffix = "us";
        private const string MillisSuffix = "ms";
        private const string SecondSuffix = "s";

        /// <summary>
        /// Attempts to convert the object into a <see cref="long"/> duration that may include a precision suffix.
        /// </summary>
        /// <param name="obj">The <see cref="object"/> to try and convert.</param>
        /// <param name="duration">The <see cref="long"/> duration of the object.</param>
        /// <returns>A <see cref="bool"/> to indicate if a conversion was possible.</returns>
        internal static bool TryConvertToMicros(object obj, out long duration)
        {
            duration = 0;
            var durationStr = obj?.ToString();
            if (string.IsNullOrWhiteSpace(durationStr))
            {
                return false;
            }

            var result = DurationValueSuffixRegex.Match(durationStr);
            if (!result.Success) // didn't parse
            {
                return false;
            }

            switch (result.Groups.Count)
            {
                case 2: // just a value
                    duration = long.Parse(result.Groups[1].Value);
                    break;
                case 3: // value & suffix
                    duration = long.Parse(result.Groups[1].Value);

                    var suffix = result.Groups[2].Value;
                    if (!string.IsNullOrWhiteSpace(suffix))
                    {
                        switch (suffix)
                        {
                            case MicrosSuffix: // don't need to change
                                break;
                            case MillisSuffix:
                                duration = duration * 1000;
                                break;
                            case SecondSuffix:
                                duration = duration * 1000000;
                                break;
                            default:
                                //Log.Debug($"Unable to convert unknown precision suffix: {suffix}");
                                break;
                        }
                    }
                    break;
            }

            return true;
        }

        /// <summary>
        /// Convert nanoseconds to ticks
        /// </summary>
        /// <param name="nanoseconds">The nanoseconds value to convert.</param>
        /// <returns>The number of ticks represented, which is probably lower resolution.</returns>
        internal static double NanosecondsToTicks(long nanoseconds)
        {
            var ticksPerNs = (double)TimeSpan.TicksPerSecond / NanosecondsPerSecond;
             return (long)(nanoseconds * ticksPerNs);
        }

        /// <summary>
        /// Convert nanoseconds to a TimeSpan
        /// </summary>
        /// <param name="nanoseconds">The nanosecond to convert</param>
        /// <returns>A TimeSpan</returns>
        internal static TimeSpan FromNanoseconds(long nanoseconds) => TimeSpan.FromTicks((long)NanosecondsToTicks(nanoseconds));
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion
