#nullable enable

using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Transcoders;

namespace Couchbase.Client.Transactions.Config;

public class TransactionInsertOptionsBuilder
{
    private ITypeTranscoder? _transcoder;
    private IRequestSpan? _span;

    public static TransactionInsertOptionsBuilder Default = Create();

    private TransactionInsertOptionsBuilder()
    {
    }
    /// <summary>
    /// Specify a specific Transcoder to use when performing the transactional GET.
    /// If none is specified, we default to JsonTranscoder
    /// </summary>
    /// <param name="transcoder"></param> the transcoder to use.
    /// <returns></returns>
    public TransactionInsertOptionsBuilder Transcoder(ITypeTranscoder transcoder)
    {
        _transcoder = transcoder;
        return this;
    }

    /// <summary>
    /// Specify a parent tracing span for this operation to use as the parent of any spans
    /// it may create.
    /// </summary>
    /// <param name="span">The desired parent span.</param>
    /// <returns></returns>
    public TransactionInsertOptionsBuilder Span(IRequestSpan span)
    {
        _span = span;
        return this;
    }

    /// <summary>
    /// Create an instance of TransactionInsertOptions from the current state of this builder.
    /// Note this is a new instance, calling Build() several times will return several independent
    /// instances of TransactionInsertOptions.
    /// </summary>
    /// <returns></returns>
    public TransactionInsertOptions Build()
    {
        return new TransactionInsertOptions(_transcoder, _span);
    }

    /// <summary>
    /// Create a new instance of TransactionInsertOptionsBuilder.
    /// </summary>
    /// <returns>A new instance of the builder.</returns>
    public static TransactionInsertOptionsBuilder Create()
    {
        return new TransactionInsertOptionsBuilder();
    }
}
