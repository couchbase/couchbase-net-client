#nullable enable
using System;
using Couchbase.Core.Compatibility;

namespace Couchbase.Core.Diagnostics.Tracing
{
    /// <summary>
    /// A span used internally by CouchbaseNetClient for tracing.
    /// </summary>
    /// <remarks>Volatile.  (This interface may change in breaking ways during minor releases)</remarks>
    [InterfaceStability(Level.Volatile)]
    public interface IInternalSpan : IRequestSpan
    {
        IInternalSpan StartPayloadEncoding();

        IInternalSpan StartDispatch();

        IInternalSpan WithTag(string key, string value);

        TimeSpan? Duration { get; }

        /// <summary>
        /// If true, this span is not performing any tracing.
        /// </summary>
        /// <remarks>
        /// This value may be checked as an optimization, for example to avoid unnecessary string formatting
        /// before a call to <see cref="WithTag"/>.
        /// </remarks>
        bool IsNullSpan { get; }
    }
}
