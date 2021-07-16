using System;
using System.Buffers;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.Compression
{
    /// <summary>
    /// Default implementation of <see cref="IOperationCompressor"/>. Applies logic and logging around compression.
    /// </summary>
    internal class OperationCompressor : IOperationCompressor
    {
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
        public IMemoryOwner<byte>? Compress(ReadOnlyMemory<byte> input)
        {
            if (!_clusterOptions.Compression || input.Length < _clusterOptions.CompressionMinSize)
            {
                _logger.LogTrace("Skipping operation compression because length {length} < {compressionMinSize}",
                    input.Length, _clusterOptions.CompressionMinSize);
                return null;
            }

            var compressed = _compressionAlgorithm.Compress(input);
            try
            {
                var compressedMemory = compressed.Memory;
                var compressionRatio = compressedMemory.Length == 0
                    ? 1f
                    : (float) compressedMemory.Length / input.Length;

                if (compressionRatio > _clusterOptions.CompressionMinRatio)
                {
                    _logger.LogTrace("Skipping operation compression because compressed size {compressionRatio} > {compressionMinRatio}",
                        compressionRatio, _clusterOptions.CompressionMinRatio);

                    // Insufficient compression, so drop it
                    compressed.Dispose();
                    return null;
                }

                return compressed;
            }
            catch
            {
                compressed.Dispose();
                throw;
            }
        }

        /// <inheritdoc />
        public IMemoryOwner<byte> Decompress(ReadOnlyMemory<byte> input) =>
            _compressionAlgorithm.Decompress(input);
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
