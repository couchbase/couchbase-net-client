#nullable enable

using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Transcoders;

namespace Couchbase.Client.Transactions.Config;

public class TransactionReplaceOptionsBuilder
{
    private ITypeTranscoder? _transcoder;
    private IRequestSpan? _span;
    private TransactionReplaceOptionsBuilder()
    {
    }

    /// <summary>
    /// This creates a new builder with the default values (if any) for this builder.
    /// </summary>
    public static TransactionReplaceOptionsBuilder Default = Create();


    /// <summary>
    /// Specify a specific Transcoder to use when performing the transactional GET.
    /// If none is specified, we default to JsonTranscoder
    /// </summary>
    /// <param name="transcoder"></param> the transcoder to use.
    /// <returns></returns>
    public TransactionReplaceOptionsBuilder Transcoder(ITypeTranscoder transcoder)
    {
        _transcoder = transcoder;
        return this;
    }

    /// <summary>
    /// Specifies a parent span for any tracing spans this operation creates.
    /// </summary>
    /// <param name="span">The desired parent tracing span.</param>
    /// <returns></returns>
    public TransactionReplaceOptionsBuilder Span(IRequestSpan span)
    {
        _span = span;
        return this;
    }

    /// <summary>
    /// Builds a new independent instance of TransactionReplaceOptions, based on the current state
    /// of this builder.   Note that if you call Build multiple times, you will have multiple
    /// independent instances of the options.
    /// </summary>
    /// <returns>A TransactionReplaceOptions instance.</returns>
    public TransactionReplaceOptions Build()
    {
        return new TransactionReplaceOptions(_transcoder, _span);
    }

    /// <summary>
    /// Creates a new instance of TransactionReplaceOptionsBuilder.
    /// </summary>
    /// <returns></returns>
    public static TransactionReplaceOptionsBuilder Create()
    {
        return new TransactionReplaceOptionsBuilder();
    }
}
