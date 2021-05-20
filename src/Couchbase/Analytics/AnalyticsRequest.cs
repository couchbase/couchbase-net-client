using Couchbase.Core.Retry;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Couchbase.Analytics
{
    internal class AnalyticsRequest : IRequest
    {
        public static AnalyticsRequest Create(string statement, AnalyticsOptions options)
        {
            return new()
            {
                    RetryStrategy = options.RetryStrategyValue,
                    Timeout = options.TimeoutValue!.Value,
                    ClientContextId = options.ClientContextIdValue,
                    Statement = statement,
                    Token = options.Token,
                    Options = options,
                    Idempotent = options.ReadonlyValue
                };
        }

        private IRetryStrategy _retryStrategy;
        public uint Attempts { get; set; }
        public bool Idempotent{ get;set; }
        public IRetryStrategy RetryStrategy
        {
            get => _retryStrategy ??= new BestEffortRetryStrategy();
            set => _retryStrategy = value;
        }
        public TimeSpan Timeout { get; set; }
        public CancellationToken Token { get; set; }
        public string ClientContextId { get; set; }
        public List<RetryReason> RetryReasons { get; set; } = new();
        public string Statement { get; set; }

        //specific to analytics
        public AnalyticsOptions Options { get; set; }
    }
}
