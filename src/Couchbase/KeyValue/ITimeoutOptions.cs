using System;
using System.Threading;

#nullable enable

namespace Couchbase.KeyValue
{
    /// <summary>
    /// Applied to key/value options which may have a cancellation token or timeout.
    /// </summary>
    internal interface ITimeoutOptions : IKeyValueOptions
    {
        internal TimeSpan? Timeout { get; }

        internal CancellationToken Token { get; }
    }
}
