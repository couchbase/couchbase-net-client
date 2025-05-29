#nullable enable

using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Transcoders;

namespace Couchbase.Client.Transactions.Config;

public class TransactionGetOptionsBuilder
{
    private ITypeTranscoder? _transcoder;
    private IRequestSpan? _span;


    private TransactionGetOptionsBuilder()
    {
    }

    /// <summary>
    /// Return a new builder with default values (if any).
    /// </summary>
    public static readonly TransactionGetOptionsBuilder Default = new();

    /// <summary>
    /// Specify a specific Transcoder to use when performing the transactional GET.
    /// If none is specified, we default to JsonTranscoder
    /// </summary>
    /// <param name="transcoder"></param> the transcoder to use.
    /// <returns></returns>
    public TransactionGetOptionsBuilder Transcoder(ITypeTranscoder transcoder)
    {
        _transcoder = transcoder;
        return this;
    }

    /// <summary>
    /// Specify an optional parent tracing span, if desired.  The tracing spans created by this
    /// call with all be children of this.
    /// </summary>
    /// <param name="span">The parent tracing span for this operation</param>
    /// <returns></returns>
    public TransactionGetOptionsBuilder Span(IRequestSpan span)
    {
        _span = span;
        return this;
    }

    /// <summary>
    /// Create an instance of TransactionGettOptions from the current state of this builder.
    /// Note this is a new instance, calling Build() several times will return several independent
    /// instances of TransactionGetOptions.    /// </summary>
    /// <returns></returns>
    public TransactionGetOptions Build()
    {
        return new(_transcoder,  _span);
    }

    public static TransactionGetOptionsBuilder Create()
    {
        return new();
    }
}
