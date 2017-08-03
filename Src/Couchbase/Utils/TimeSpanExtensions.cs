using System;

namespace Couchbase.Utils
{
    public static class TimeSpanExtensions
    {
        /// <summary>
        /// Converts a <see cref="TimeSpan" /> into an uint correctly representing a Time-To-Live,
        /// that is expressed in seconds.
        /// Durations strictly bigger than 30 days are converted to a unix-syle timestamp (seconds since the Epoch),
        /// as described in the couchbase TTL documentation.
        /// </summary>
        /// <returns>The TTL, expressed as a suitable uint.</returns>
        public static uint ToTtl(this TimeSpan duration)
        {
            if (duration <= TimeSpan.FromDays(30))
            {
                return (uint)duration.TotalSeconds;
            }
            else
            {
                var dateExpiry = DateTime.UtcNow + duration;
                var unixTimeStamp = (uint) (dateExpiry.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                return unixTimeStamp;
            }
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
    }
}
