using System;

namespace RetryExample
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

        public bool NoRetry => !Duration.HasValue;
    }
}
