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

        /// <summary>
        /// Retrieves the number of seconds expressed in a <see cref="TimeSpan"/> as an <see cref="uint"/>.
        /// </summary>
        /// <param name="timeSpan">The timespan.</param>
        /// <returns>An <see cref="uint"/> that is the total number of seconds in the <see cref="TimeSpan"/>.</returns>
        public static uint GetSeconds(this TimeSpan timeSpan)
        {
            return (uint) timeSpan.TotalSeconds;
        }
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
