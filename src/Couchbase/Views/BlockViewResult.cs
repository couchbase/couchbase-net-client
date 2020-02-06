using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Serializers;
using OpenTracing;

#nullable enable

namespace Couchbase.Views
{
    /// <summary>
    /// The result of a view query, read without streaming results.
    /// </summary>
    /// <typeparam name="TKey">Type of the key for each result row.</typeparam>
    /// <typeparam name="TValue">Type of the value for each result row.</typeparam>
    /// <seealso cref="IViewResult{TKey, TValue}" />
    internal class BlockViewResult<TKey, TValue> : ViewResultBase<TKey, TValue>
    {
        private readonly ITypeSerializer _deserializer;

        private bool _enumerated;
        private IEnumerable<IViewRow<TKey, TValue>>? _rows;

        /// <summary>
        /// Creates a new BlockViewResult.
        /// </summary>
        /// <param name="statusCode">HTTP status code returned with result.</param>
        /// <param name="message">Message about result.</param>
        /// <param name="deserializer"><see cref="ITypeSerializer"/> used to deserialize objects.</param>
        public BlockViewResult(HttpStatusCode statusCode, string message, ITypeSerializer deserializer)
            : base(statusCode, message)
        {
            _deserializer = deserializer ?? throw new ArgumentNullException(nameof(deserializer));
        }

        /// <summary>
        /// Creates a new BlockViewResult.
        /// </summary>
        /// <param name="statusCode">HTTP status code returned with result.</param>
        /// <param name="message">Message about result.</param>
        /// <param name="responseStream"><see cref="Stream"/> to read.</param>
        /// <param name="deserializer"><see cref="ITypeSerializer"/> used to deserialize objects.</param>
        /// <param name="decodeSpan">Span to complete once decoding is done.</param>
        public BlockViewResult(HttpStatusCode statusCode, string message, Stream responseStream, ITypeSerializer deserializer,
            ISpan? decodeSpan = null)
            : base(statusCode, message, responseStream, decodeSpan)
        {
            _deserializer = deserializer ?? throw new ArgumentNullException(nameof(deserializer));
        }

        /// <inheritdoc />
        public override async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_rows != null)
            {
                throw new InvalidOperationException("Cannot initialize more than once.");
            }

            if (ResponseStream != null)
            {
                var body = await _deserializer.DeserializeAsync<ViewResultData>(ResponseStream, cancellationToken);

                MetaData = new ViewMetaData
                {
                    TotalRows = body.total_rows
                };

                _rows = body.rows?.Select(p => new ViewRow<TKey, TValue>
                {
                    Id = p.id,
                    Key = p.key,
                    Value = p.value
                }) ?? Enumerable.Empty<IViewRow<TKey, TValue>>();
            }
            else
            {
                _rows = Enumerable.Empty<IViewRow<TKey, TValue>>();
            }

            DecodeSpan?.Finish();
        }

        /// <inheritdoc />
        public override IAsyncEnumerator<IViewRow<TKey, TValue>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            if (_rows == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(BlockViewResult<TKey, TValue>)} has not been initialized, call InitializeAsync first");
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
        // ReSharper disable UnusedAutoPropertyAccessor.Local
        // ReSharper disable AutoPropertyCanBeMadeGetOnly.Local
        internal class ViewResultData
        {
            public uint total_rows { get; set; }
            public IEnumerable<ViewRowData>? rows { get; set; }
            public string? error { get; set; }
            public string? reason { get; set; }
        }

        internal class ViewRowData
        {
            public string? id { get; set; }
            [AllowNull] public TKey key { get; set; } = default!;
            [AllowNull] public TValue value { get; set; } = default!;
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
