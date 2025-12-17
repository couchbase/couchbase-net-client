#nullable enable

using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Transcoders;

namespace Couchbase.Client.Transactions.Config;

public class TransactionGetOptions
{
    internal TransactionGetOptions(ITypeTranscoder? transcoder, IRequestSpan? span)
    {
        Transcoder = transcoder;
        Span = span;
    }

    /// <summary>
    /// This is used to set the transcoder you would like to use for this data.
    /// </summary>
    public ITypeTranscoder? Transcoder { get; init; }

    /// <summary>
    /// Used as the parent span for any tracing span this operation creates.
    /// </summary>
    public IRequestSpan? Span { get; init; }
}
