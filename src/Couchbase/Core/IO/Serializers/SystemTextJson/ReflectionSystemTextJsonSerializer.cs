using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Utils;

#nullable enable

// ReSharper disable once CheckNamespace
namespace Couchbase.Core.IO.Serializers.SystemTextJson
{
    /// <summary>
    /// A JSON serializer which uses reflection.
    /// </summary>
    internal class ReflectionSystemTextJsonSerializer : SystemTextJsonSerializer
    {
        internal const string SerializationUnreferencedCodeMessage = "JSON serialization and deserialization might require types that cannot be statically analyzed. Use the overload that takes a JsonTypeInfo or JsonSerializerContext, or make sure all of the required types are preserved.";

        /// <inheritdoc />
        public override JsonSerializerOptions Options { get; }

        #region ctor

        /// <summary>
        /// Create a new SystemTextJsonSerializer with default options, optionally enabling increased Newtonsoft.Json compatibility.
        /// </summary>
        /// <param name="increasedNewtonsoftCompatibility">Enable increased Newtonsoft.Json compatibility.</param>
        /// <remarks>
        /// The <paramref name="increasedNewtonsoftCompatibility"/> parameter doesn't make this fully compatible with Newtonsoft.Json.
        /// However, it does enable several features to make it more compatible such as case-insensitive property name deserialization,
        /// ignoring comments and trailcommas, serializing public fields, etc. For details on compatibility, see
        /// https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-migrate-from-newtonsoft-how-to
        /// </remarks>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        public ReflectionSystemTextJsonSerializer(bool increasedNewtonsoftCompatibility = false)
            : this(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // This setting is always on by default
                AllowTrailingCommas = increasedNewtonsoftCompatibility,
                IncludeFields = increasedNewtonsoftCompatibility,
                NumberHandling = increasedNewtonsoftCompatibility ? JsonNumberHandling.AllowReadingFromString : JsonNumberHandling.Strict,
                PropertyNameCaseInsensitive = increasedNewtonsoftCompatibility,
                ReadCommentHandling = increasedNewtonsoftCompatibility ? JsonCommentHandling.Skip : JsonCommentHandling.Disallow
            })
        {
        }

        /// <summary>
        /// Create a new SystemTextJsonSerializer with supplied <see cref="JsonSerializerOptions"/>.
        /// </summary>
        /// <param name="options"><see cref="JsonSerializerOptions"/> to control serialization and deserialization.</param>
        public ReflectionSystemTextJsonSerializer(JsonSerializerOptions options)
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
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
            Justification = "This type may not be constructed without encountering a warning.")]
        public override T? Deserialize<T>(ReadOnlyMemory<byte> buffer) where T : default
        {
            // Non-stream overloads of JsonSerializer.Deserialize do not trim the BOM automatically, do this for consistency with Newtonsoft.Json
            var span = Utf8Helpers.TrimBomIfPresent(buffer.Span);

            if (span.Length == 0)
            {
                // Replicate the Newtonsoft.Json behavior of returning the default if the buffer is empty
                return default;
            }

            return JsonSerializer.Deserialize<T>(span, Options);
        }

        /// <inheritdoc />
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
            Justification = "This type may not be constructed without encountering a warning.")]
        public override T? Deserialize<T>(Stream stream) where T : default
        {
            if (stream.CanSeek && stream.Length == 0)
            {
                // Replicate the Newtonsoft.Json behavior of returning the default if the buffer is empty
                return default;
            }

            return JsonSerializer.Deserialize<T>(stream, Options);
        }

        /// <inheritdoc />
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
            Justification = "This type may not be constructed without encountering a warning.")]
        public override ValueTask<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default) where T : default
        {
            if (stream.CanSeek && stream.Length == 0)
            {
                // Replicate the Newtonsoft.Json behavior of returning the default if the buffer is empty
                return default;
            }

            return JsonSerializer.DeserializeAsync<T>(stream, Options, cancellationToken);
        }

        #endregion

        #region Serialize

        /// <inheritdoc />
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
            Justification = "This type may not be constructed without encountering a warning.")]
        public override void Serialize(Stream stream, object? obj)
        {
            JsonSerializer.Serialize(stream, obj, Options);
        }

        /// <inheritdoc />
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
            Justification = "This type may not be constructed without encountering a warning.")]
        public override ValueTask SerializeAsync(Stream stream, object? obj, CancellationToken cancellationToken = default)
        {
            return new ValueTask(JsonSerializer.SerializeAsync(stream, obj, Options, cancellationToken));
        }

        /// <inheritdoc />
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
            Justification = "This type may not be constructed without encountering a warning.")]
        public override void Serialize<T>(Stream stream, T obj)
        {
            JsonSerializer.Serialize(stream, obj, Options);
        }

        /// <inheritdoc />
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
            Justification = "This type may not be constructed without encountering a warning.")]
        public override ValueTask SerializeAsync<T>(Stream stream, T obj, CancellationToken cancellationToken = default)
        {
            return new ValueTask(JsonSerializer.SerializeAsync(stream, obj, Options, cancellationToken));
        }

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
