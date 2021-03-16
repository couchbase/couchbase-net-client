using System;
using Couchbase.Core.IO.Compression;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Retry;
using Couchbase.KeyValue;
using Microsoft.Extensions.ObjectPool;

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
        private readonly ObjectPool<OperationBuilder> _operationBuilderPool;
        private readonly IRetryStrategy _retryStrategy;

        public OperationConfigurator(ITypeTranscoder typeTranscoder, IOperationCompressor operationCompressor,
            ObjectPool<OperationBuilder> operationBuilderPool, IRetryStrategy retryStrategy)
        {
            _typeTranscoder = typeTranscoder ?? throw new ArgumentNullException(nameof(typeTranscoder));
            _operationCompressor = operationCompressor ?? throw new ArgumentNullException(nameof(operationCompressor));
            _operationBuilderPool = operationBuilderPool;
            _retryStrategy = retryStrategy ?? throw new ArgumentNullException(nameof(retryStrategy));
        }

        /// <inheritdoc />
        public void Configure(OperationBase operation, IKeyValueOptions? options = null)
        {
            operation.Transcoder = (options as ITranscoderOverrideOptions)?.Transcoder ?? _typeTranscoder;
            operation.OperationCompressor = _operationCompressor;
            operation.OperationBuilderPool = _operationBuilderPool;
            operation.RetryStrategy = options?.RetryStrategy ?? _retryStrategy;
        }
    }
}
