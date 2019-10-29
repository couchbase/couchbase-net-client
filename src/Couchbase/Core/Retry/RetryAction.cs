using System;

namespace Couchbase.Core.Retry
{
    public class RetryAction
    {
        public RetryAction(TimeSpan? duration)
        {
            Duration = duration;
        }

        public TimeSpan? Duration { get; }

        public static RetryAction WithDuration(TimeSpan? duration)
        {
            return new RetryAction(duration);
        }

        public bool Retry => Duration.HasValue;
    }
}
