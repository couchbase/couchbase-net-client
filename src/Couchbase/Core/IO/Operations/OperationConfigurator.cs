using System;
using Couchbase.Core.IO.Transcoders;
using Couchbase.KeyValue;

#nullable enable

namespace Couchbase.Core.IO.Operations
{
    /// <summary>
    /// Default implementation of <see cref="IOperationConfigurator"/>.
    /// </summary>
    internal class OperationConfigurator : IOperationConfigurator
    {
        private readonly ITypeTranscoder _typeTranscoder;

        public OperationConfigurator(ITypeTranscoder typeTranscoder)
        {
            _typeTranscoder = typeTranscoder ?? throw new ArgumentNullException(nameof(typeTranscoder));
        }

        /// <inheritdoc />
        public void Configure(OperationBase operation, IKeyValueOptions? options = null)
        {
            operation.Transcoder = (options as ITranscoderOverrideOptions)?.Transcoder ?? _typeTranscoder;
        }
    }
}
