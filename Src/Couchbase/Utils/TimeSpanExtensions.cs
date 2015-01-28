using Couchbase.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
