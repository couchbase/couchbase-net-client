using System;

namespace Couchbase.Core.Retry
{
    public class RetryAction
    {
        public RetryAction(TimeSpan? duration)
        {
            DurationValue = duration;
        }

        public TimeSpan? DurationValue { get; }

        public static RetryAction Duration(TimeSpan? duration)
        {
            return new RetryAction(duration);
        }

        public bool Retry => DurationValue.HasValue;
    }
}
