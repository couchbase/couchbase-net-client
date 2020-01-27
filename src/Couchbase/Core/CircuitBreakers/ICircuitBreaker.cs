using System;

namespace Couchbase.Core.CircuitBreakers
{
    internal interface ICircuitBreaker
    {
        CircuitBreakerState State { get; }
        bool Enabled { get; }
        TimeSpan CanaryTimeout { get; }
        Func<Exception, bool> CompletionCallback { get; }
        bool AllowsRequest();
        void MarkSuccess();
        void MarkFailure();
        void Reset();
        void Track();
    }
}
