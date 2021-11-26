using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Serializers;
using Couchbase.Query;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Analytics
{
    /// <summary>
    /// The result of a N1QL query, read without streaming results.
    /// </summary>
    /// <typeparam name="T">The Type of each row returned.</typeparam>
    /// <seealso cref="IAnalyticsResult{T}" />
    internal class BlockAnalyticsResult<T> : AnalyticsResultBase<T>
    {
        private readonly ITypeSerializer _deserializer;

        private bool _enumerated;
        private IEnumerable<T>? _rows;

        /// <summary>
        /// Creates a new BlockAnalyticsResult.
        /// </summary>
        /// <param name="responseStream"><see cref="Stream"/> to read.</param>
        /// <param name="deserializer"><see cref="ITypeSerializer"/> used to deserialize objects.</param>
        public BlockAnalyticsResult(Stream responseStream, ITypeSerializer deserializer)
            : base(responseStream)
        {
            _deserializer = deserializer ?? throw new ArgumentNullException(nameof(deserializer));
        }

        /// <inheritdoc />
        public override async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            var body = await _deserializer.DeserializeAsync<AnalyticsResultData>(ResponseStream, cancellationToken)
                .ConfigureAwait(false);
            if (body == null)
            {
                ThrowHelper.ThrowInvalidOperationException("No data received.");
            }

            MetaData = new AnalyticsMetaData
            {
                RequestId = body.requestID,
                ClientContextId = body.clientContextID,
                Signature = body.signature,
                Metrics = body.metrics?.ToMetrics() ?? new AnalyticsMetrics()
            };

            if (Enum.TryParse(body.status, true, out AnalyticsStatus status))
            {
                MetaData.Status = status;
            }

            if (body.warnings != null)
            {
                MetaData.Warnings.AddRange(body.warnings.Select(p => p.ToWarning()));
            }
            if (body.errors != null)
            {
                Errors.AddRange(body.errors.Select(p => p.ToError()));
            }

            _rows = body.results ?? Enumerable.Empty<T>();
        }

        /// <inheritdoc />
        public override IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            if (_rows == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(BlockAnalyticsResult<T>)} has not been initialized, call InitializeAsync first");
            }

            if (_enumerated)
            {
                throw new StreamAlreadyReadException();
            }

            _enumerated = true;

            return _rows.ToAsyncEnumerable().GetAsyncEnumerator(cancellationToken);
        }

        // ReSharper disable ClassNeverInstantiated.Local
        // ReSharper disable InconsistentNaming
        // ReSharper disable UnusedMember.Local
        // ReSharper disable UnusedAutoPropertyAccessor.Local
        private class AnalyticsResultData
        {
            public string? requestID { get; set; }
            public string? clientContextID { get; set; }
            public dynamic? signature { get; set; }
            public IEnumerable<T>? results { get; set; }
            public string? status { get; set; }
            public IEnumerable<ErrorData>? errors { get; set; }
            public IEnumerable<WarningData>? warnings { get; set; }
            public MetricsData? metrics { get; set; }
            public string? handle { get; set; }
        }
    }
}
#region [ License information ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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

#endregion
