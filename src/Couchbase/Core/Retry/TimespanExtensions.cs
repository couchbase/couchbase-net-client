using System;

namespace Couchbase.Core.Retry
{
    internal static class TimespanExtensions
    {
        public static TimeSpan CappedDuration(this TimeSpan lifetime, TimeSpan duration, TimeSpan elapsed)
        {
            if (elapsed >= lifetime)
            {
                return TimeSpan.Zero;
            }

            if (elapsed + duration >= lifetime)
            {
                return lifetime - elapsed;
            }

            return duration;
        }
    }
}
