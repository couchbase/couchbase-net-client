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
        /// </summary>
        /// <returns>The TTL, expressed as a suitable uint.</returns>
        public static uint ToTtl(this TimeSpan duration)
        {
            return (uint)duration.TotalSeconds;
        }
    }
}
