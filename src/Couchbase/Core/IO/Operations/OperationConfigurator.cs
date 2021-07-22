using System;
using System.Globalization;
using Couchbase.Core.Diagnostics.Tracing;
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

            if (operation.Span.CanWrite && options is ITimeoutOptions options1)
            {
                operation.Span.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.TimeoutMilliseconds,
                    options1.Timeout?.TotalMilliseconds.ToString(CultureInfo.InvariantCulture)!);
            }
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
