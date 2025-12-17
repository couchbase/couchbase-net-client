using System;
using System.Buffers;

namespace Couchbase.Utils
{
    internal static class ReadOnlySequenceExtensions
    {
        /// <summary>
        /// Gets the first span from a <see cref="ReadOnlySequence{T}"/> in the most efficient way available
        /// on any .NET runtime.
        /// </summary>
        public static ReadOnlySpan<T> GetFirstSpan<T>(this ReadOnlySequence<T> sequence) =>
            // Due to it's simplicity, either this method should be inlined into the call site, or the property
            // access should be inlined into this method, or the property access should be inlined all the way to
            // the call site. In any case, the JIT should be able to optimize this method away.
#if SPAN_SUPPORT
            sequence.FirstSpan;
#else
            sequence.First.Span;
#endif
    }
}
