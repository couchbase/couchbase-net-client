using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.Serializers.SystemTextJson
{
    /// <summary>
    /// A JSON serializer which uses <see cref="JsonSerializerContext"/> to support higher performance and trimming.
    /// </summary>
    internal class ContextSystemTextJsonSerializer : SystemTextJsonSerializer
    {
        /// <inheritdoc />
        public override JsonSerializerOptions Options => Context.Options;

        /// <summary>
        /// <see cref="JsonSerializerContext"/> provided during construction.
        /// </summary>
        public JsonSerializerContext Context { get; }

        #region ctor

        /// <summary>
        /// Create a new SystemTextJsonSerializer using a supplied <see cref="JsonSerializerContext"/>.
        /// </summary>
        /// <param name="context"><see cref="JsonSerializerOptions"/> to use for serialization and deserialization.</param>
        /// <remarks>
        /// This overload should be used to supply custom serializers on a per-request basis that are optimized for the particular
        /// type being serialized or deserialized. Any type which isn't registered in the <see cref="JsonSerializerContext"/>
        /// will be handled using the <see cref="JsonSerializerContext.Options"/>.
        /// </remarks>
        public ContextSystemTextJsonSerializer(JsonSerializerContext context)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (context == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(context));
            }

            Context = context;
        }

        #endregion

        #region Deserialization

        /// <inheritdoc />
        public override T? Deserialize<T>(ReadOnlyMemory<byte> buffer) where T : default
        {
            // Non-stream overloads of JsonSerializer.Deserialize do not trim the BOM automatically, do this for consistency with Newtonsoft.Json
            var span = Utf8Helpers.TrimBomIfPresent(buffer.Span);

            if (span.Length == 0)
            {
                // Replicate the Newtonsoft.Json behavior of returning the default if the buffer is empty
                return default;
            }

            var typeInfo = Context.GetTypeInfo<T>();

            return JsonSerializer.Deserialize<T>(span, typeInfo);
        }

        /// <inheritdoc />
        public override T? Deserialize<T>(Stream stream) where T : default
        {
            if (stream.CanSeek && stream.Length == 0)
            {
                // Replicate the Newtonsoft.Json behavior of returning the default if the buffer is empty
                return default;
            }

            var typeInfo = Context.GetTypeInfo<T>();

            return JsonSerializer.Deserialize<T>(stream, typeInfo);
        }

        /// <inheritdoc />
        public override ValueTask<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default) where T : default
        {
            if (stream.CanSeek && stream.Length == 0)
            {
                // Replicate the Newtonsoft.Json behavior of returning the default if the buffer is empty
                return default;
            }

            var typeInfo = Context.GetTypeInfo<T>();

            return JsonSerializer.DeserializeAsync<T>(stream, typeInfo, cancellationToken);
        }

        #endregion

        #region Serialize

        /// <inheritdoc />
        /// <remarks>
        /// This overload does not make use of <see cref="Context"/>.
        /// </remarks>
        public override void Serialize(Stream stream, object? obj)
        {
            JsonSerializer.Serialize(stream, obj, obj?.GetType() ?? typeof(object), Context);
        }

        /// <inheritdoc />
        /// <remarks>
        /// This overload does not make use of <see cref="Context"/>.
        /// </remarks>
        public override ValueTask SerializeAsync(Stream stream, object? obj, CancellationToken cancellationToken = default)
        {
            return new ValueTask(JsonSerializer.SerializeAsync(stream, obj, obj?.GetType() ?? typeof(object), Context, cancellationToken));
        }

        /// <inheritdoc />
        public override void Serialize<T>(Stream stream, T obj)
        {
            var typeInfo = Context.GetTypeInfo<T>();

            JsonSerializer.Serialize(stream, obj, typeInfo);
        }

        /// <inheritdoc />
        public override ValueTask SerializeAsync<T>(Stream stream, T obj, CancellationToken cancellationToken = default)
        {
            var typeInfo = Context.GetTypeInfo<T>();

            return new ValueTask(JsonSerializer.SerializeAsync(stream, obj, typeInfo, cancellationToken));
        }

        #endregion

        #region Projection

        public override IProjectionBuilder CreateProjectionBuilder(ILogger logger) =>
            new ContextSystemTextJsonProjectionBuilder(Context, logger);

        #endregion

        #region Streaming

        public override IJsonStreamReader CreateJsonStreamReader(Stream stream) =>
            new ContextSystemTextJsonStreamReader(stream, Context);

        #endregion
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
