using System;
using System.Buffers;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.Serializers.SystemTextJson
{
    /// <summary>
    /// A JSON serializer which uses <see cref="JsonSerializerOptions.GetTypeInfo(Type)"/> to support
    /// serialization and deserialization. This can work with constructed <see cref="JsonSerializerOptions"/> with
    /// a custom <see cref="JsonSerializerOptions.TypeInfoResolver"/> or with options directly from a
    /// <see cref="JsonSerializerContext"/>.
    /// </summary>
    /// <remarks>
    /// When using a constructed <see cref="JsonSerializerOptions"/>, by default it has a <c>null</c>
    /// <see cref="JsonSerializerOptions.TypeInfoResolver"/>. <see cref="JsonSerializerOptions.MakeReadOnly(bool)"/>
    /// should be called before passing the options to this class to ensure the <see cref="DefaultJsonTypeInfoResolver"/>
    /// is registered.
    /// </remarks>
    internal sealed class TypeInfoSystemTextJsonSerializer : SystemTextJsonSerializer
    {
        /// <inheritdoc />
        public override JsonSerializerOptions Options { get; }

        #region ctor

        /// <summary>
        /// Create a new SystemTextJsonSerializer using a supplied <see cref="JsonSerializerOptions"/>.
        /// </summary>
        /// <param name="options"><see cref="JsonSerializerOptions"/> to use for serialization and deserialization.</param>
        public TypeInfoSystemTextJsonSerializer(JsonSerializerOptions options)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (options == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(options));
            }

            Options = options;
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

            var typeInfo = (JsonTypeInfo<T>)Options.GetTypeInfo(typeof(T));

            return JsonSerializer.Deserialize<T>(span, typeInfo);
        }

        /// <inheritdoc />
        public override T? Deserialize<T>(ReadOnlySequence<byte> buffer) where T : default
        {
            // Non-stream overloads of JsonSerializer.Deserialize do not trim the BOM automatically, do this for consistency with Newtonsoft.Json
            buffer = Utf8Helpers.TrimBomIfPresent(buffer);

            if (buffer.Length == 0)
            {
                // Replicate the Newtonsoft.Json behavior of returning the default if the buffer is empty
                return default;
            }

            var typeInfo = (JsonTypeInfo<T>)Options.GetTypeInfo(typeof(T));

            var reader = new Utf8JsonReader(buffer, GetJsonReaderOptions(typeInfo.Options));

            return JsonSerializer.Deserialize<T>(ref reader, typeInfo);
        }

        /// <inheritdoc />
        public override T? Deserialize<T>(Stream stream) where T : default
        {
            if (stream.CanSeek && stream.Length == 0)
            {
                // Replicate the Newtonsoft.Json behavior of returning the default if the buffer is empty
                return default;
            }

            var typeInfo = (JsonTypeInfo<T>)Options.GetTypeInfo(typeof(T));

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

            var typeInfo = (JsonTypeInfo<T>)Options.GetTypeInfo(typeof(T));

            return JsonSerializer.DeserializeAsync<T>(stream, typeInfo, cancellationToken);
        }

        #endregion

        #region Serialize

        /// <inheritdoc />
        public override void Serialize(Stream stream, object? obj)
        {
            var typeInfo = Options.GetTypeInfo(obj?.GetType() ?? typeof(object));

            JsonSerializer.Serialize(stream, obj, typeInfo);
        }

        /// <inheritdoc />
        public override ValueTask SerializeAsync(Stream stream, object? obj, CancellationToken cancellationToken = default)
        {
            var typeInfo = Options.GetTypeInfo(obj?.GetType() ?? typeof(object));

            return new ValueTask(JsonSerializer.SerializeAsync(stream, obj, typeInfo, cancellationToken));
        }

        /// <inheritdoc />
        public override void Serialize(Utf8JsonWriter writer, object? obj)
        {
            var typeInfo = Options.GetTypeInfo(obj?.GetType() ?? typeof(object));

            JsonSerializer.Serialize(writer, obj, typeInfo);
        }

        /// <inheritdoc />
        public override void Serialize<T>(Stream stream, T obj)
        {
            var typeInfo = (JsonTypeInfo<T>) Options.GetTypeInfo(typeof(T));

            JsonSerializer.Serialize(stream, obj, typeInfo);
        }

        /// <inheritdoc />
        public override void Serialize<T>(IBufferWriter<byte> writer, T obj)
        {
            var typeInfo = (JsonTypeInfo<T>) Options.GetTypeInfo(typeof(T));

            using var jsonWriter = new Utf8JsonWriter(writer, GetJsonWriterOptions(typeInfo.Options));

            JsonSerializer.Serialize(jsonWriter, obj, typeInfo);
        }

        /// <inheritdoc />
        public override ValueTask SerializeAsync<T>(Stream stream, T obj, CancellationToken cancellationToken = default)
        {
            var typeInfo = (JsonTypeInfo<T>) Options.GetTypeInfo(typeof(T));

            return new ValueTask(JsonSerializer.SerializeAsync(stream, obj, typeInfo, cancellationToken));
        }

        /// <inheritdoc />
        public override void Serialize<T>(Utf8JsonWriter writer, T obj)
        {
            var typeInfo = (JsonTypeInfo<T>) Options.GetTypeInfo(typeof(T));

            JsonSerializer.Serialize(writer, obj, typeInfo);
        }

        #endregion

        #region Projection

        public override IProjectionBuilder CreateProjectionBuilder(ILogger logger) =>
            new SystemTextJsonProjectionBuilder(Options);

        #endregion

        #region Streaming

        public override IJsonStreamReader CreateJsonStreamReader(Stream stream) =>
            new SystemTextJsonStreamReader(stream, Options);

        #endregion

        /// <inheritdoc />
        public override bool CanSerialize(Type type) => Options.TryGetTypeInfo(type, out _);
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
