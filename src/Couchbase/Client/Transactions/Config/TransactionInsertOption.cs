#nullable enable

using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Transcoders;

namespace Couchbase.Client.Transactions.Config;

public class TransactionInsertOptions
{
    internal TransactionInsertOptions(ITypeTranscoder? transcoder, IRequestSpan? span)
    {
        Transcoder = transcoder;
        Span = span;
    }

    /// <summary>
    /// This is used to set the transcoder you would like to use for this data.
    /// </summary>
    public ITypeTranscoder? Transcoder { get; init; }

    /// <summary>
    /// This is used as the parent span for all tracing spans created by this operation.
    /// </summary>
    public IRequestSpan? Span { get; init; }
}
