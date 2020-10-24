using System;
using Couchbase.Core.IO.Compression;
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
        private readonly IOperationCompressor _operationCompressor;

        public OperationConfigurator(ITypeTranscoder typeTranscoder, IOperationCompressor operationCompressor)
        {
            _typeTranscoder = typeTranscoder ?? throw new ArgumentNullException(nameof(typeTranscoder));
            _operationCompressor = operationCompressor ?? throw new ArgumentNullException(nameof(operationCompressor));
        }

        /// <inheritdoc />
        public void Configure(OperationBase operation, IKeyValueOptions? options = null)
        {
            operation.Transcoder = (options as ITranscoderOverrideOptions)?.Transcoder ?? _typeTranscoder;
            operation.OperationCompressor = _operationCompressor;
        }
    }
}
