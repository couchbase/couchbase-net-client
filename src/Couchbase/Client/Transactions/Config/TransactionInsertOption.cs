#nullable enable

using System;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Transcoders;

namespace Couchbase.Client.Transactions.Config;

public class TransactionInsertOptions
{
    internal TransactionInsertOptions(ITypeTranscoder? transcoder, IRequestSpan? span, TimeSpan? expiry)
    {
        Transcoder = transcoder;
        Span = span;
        Expiry = expiry;
    }

    /// <summary>
    /// This is used to set the transcoder you would like to use for this data.
    /// </summary>
    public ITypeTranscoder? Transcoder { get; init; }

    /// <summary>
    /// This is used as the parent span for all tracing spans created by this operation.
    /// </summary>
    public IRequestSpan? Span { get; init; }

    /// <summary>
    /// If set, the document will expire after the specified time.
    /// </summary>
    public TimeSpan? Expiry {get; init;}
}
