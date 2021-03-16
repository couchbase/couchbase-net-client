using Couchbase.Core.Retry;

#nullable enable

namespace Couchbase.KeyValue
{
    internal interface IKeyValueOptions
    {
        internal IRetryStrategy? RetryStrategy { get; }
    }
}
