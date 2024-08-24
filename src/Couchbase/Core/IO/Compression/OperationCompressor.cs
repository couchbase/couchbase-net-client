using System;
using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using Couchbase.Core.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.Compression
{
    /// <summary>
    /// Default implementation of <see cref="IOperationCompressor"/>. Applies logic and logging around compression.
    /// </summary>
    internal sealed class OperationCompressor : IOperationCompressor
    {
        private static readonly Action<ILogger, int, int, Exception?> LogSkipLength =
            LoggerMessage.Define<int, int>(LogLevel.Trace, 0,
                "Skipping operation compression because length {length} < {compressionMinSize}");

        private static readonly Action<ILogger, float, float, Exception?> LogSkipCompressionRatio =
            LoggerMessage.Define<float, float>(LogLevel.Trace, 1,
                "Skipping operation compression because compressed size {compressionRatio} > {compressionMinRatio}");

        private readonly ICompressionAlgorithm _compressionAlgorithm;
        private readonly ClusterOptions _clusterOptions;
        private readonly ILogger<OperationCompressor> _logger;

        public OperationCompressor(ICompressionAlgorithm compressionAlgorithm, ClusterOptions clusterOptions,
            ILogger<OperationCompressor> logger)
        {
            _compressionAlgorithm = compressionAlgorithm ?? throw new ArgumentNullException(nameof(compressionAlgorithm));
            _clusterOptions = clusterOptions ?? throw new ArgumentNullException(nameof(clusterOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public IMemoryOwner<byte>? Compress(ReadOnlyMemory<byte> input, IRequestSpan parentSpan)
        {
            if (!_clusterOptions.Compression || input.Length < _clusterOptions.CompressionMinSize)
            {
                LogSkipLength(_logger, input.Length, _clusterOptions.CompressionMinSize, null);
                return null;
            }

            using var compressionSpan = parentSpan.CompressionSpan();

            var compressed = _compressionAlgorithm.Compress(input);
            try
            {
                var compressedMemory = compressed.Memory;
                var compressionRatio = compressedMemory.Length == 0
                    ? 1f
                    : (float) compressedMemory.Length / input.Length;

                if (compressionRatio > _clusterOptions.CompressionMinRatio)
                {
                    AddCompressionTags(compressionSpan, compressionRatio, false);

                    LogSkipCompressionRatio(_logger, compressionRatio, _clusterOptions.CompressionMinRatio, null);

                    // Insufficient compression, so drop it
                    compressed.Dispose();
                    return null;
                }

                AddCompressionTags(compressionSpan, compressionRatio, true);

                return compressed;
            }
            catch
            {
                compressed.Dispose();
                throw;
            }
        }

        /// <inheritdoc />
        public IMemoryOwner<byte> Decompress(ReadOnlyMemory<byte> input, IRequestSpan parentSpan)
        {
            using var _ = parentSpan.DecompressionSpan();

            return _compressionAlgorithm.Decompress(input);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddCompressionTags(IRequestSpan span, double ratio, bool used)
        {
            if (span.CanWrite)
            {
                span.SetAttribute(InnerRequestSpans.CompressionSpan.Attributes.CompressionRatio,
                    ratio.ToString("0.0000", CultureInfo.InvariantCulture));
                span.SetAttribute(InnerRequestSpans.CompressionSpan.Attributes.CompressionUsed,
                    used);
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
