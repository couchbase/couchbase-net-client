#nullable enable
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Retry;

namespace Couchbase.Analytics
{
    public class AnalyticsRequest : RequestBase
    {
        public static AnalyticsRequest Create(string statement, IValueRecorder recorder, AnalyticsOptions options)
        {
            return new(options.ReadonlyValue)
            {
                    RetryStrategy = options.RetryStrategyValue ?? new BestEffortRetryStrategy(),
                    Timeout = options.TimeoutValue!.Value,
                    ClientContextId = options.ClientContextIdValue,
                    Statement = statement,
                    Token = options.Token,
                    Options = options,
                    Recorder = recorder
                };
        }

        public AnalyticsRequest(bool idempotent)
        {
            Idempotent = idempotent;
        }

        public override bool Idempotent { get; }

        //specific to analytics
        public AnalyticsOptions? Options { get; set; }
    }
}
