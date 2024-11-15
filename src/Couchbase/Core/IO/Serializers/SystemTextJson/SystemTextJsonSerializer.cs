using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Compatibility;
using Couchbase.Core.IO.Serializers.SystemTextJson;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

// ReSharper disable once CheckNamespace
namespace Couchbase.Core.IO.Serializers
{
    /// <summary>
    /// A JSON serializer based on System.Text.Json.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     This class is currently experimental and subject to change. It does not support all serialization features
    ///     supported by the <see cref="DefaultSerializer"/>. Known limitations currently include:
    /// </para>
    /// <list type="bullet">
    ///     <item>
    ///         <description>
    ///             Couchbase.Transactions is not currently supported.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <description>
    ///             No support for <c>dynamic</c> types.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <description>
    ///             Some properties of <see cref="Couchbase.Query.QueryMetaData"/> which use <c>dynamic</c>,
    ///             such as <see cref="Couchbase.Query.QueryMetaData.Profile"/> and <see cref="Couchbase.Query.QueryMetaData.Signature"/>,
    ///             are not populated.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <description>
    ///             View-style queries are not supported using <c>JsonSerializerContext</c>, only reflection-based serialization.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <description>
    ///             Any use of <see cref="Newtonsoft.Json.Linq.JToken"/> should be replaced with <see cref="JsonElement"/> or <c>object</c>.
    ///         </description>
    ///     </item>
    /// </list>
    /// </remarks>
    [InterfaceStability(Level.Volatile)]
    public abstract class SystemTextJsonSerializer : IExtendedTypeSerializer, IProjectableTypeDeserializer, IStreamingTypeDeserializer, IBufferedTypeSerializer
    {
        private const string SerializationUnreferencedCodeMessage =
            "JSON serialization and deserialization might require types that cannot be statically analyzed. Use the overload that takes a JsonSerializerContext, or make sure all of the required types are preserved.";
        private const string SerializationDynamicCodeMessage =
            "JSON serialization and deserialization might require types that cannot be statically analyzed. Use the overload that takes a JsonSerializerContext.";

        private static readonly SupportedDeserializationOptions SupportedDeserializationOptionsStatic = new();

        /// <summary>
        /// <see cref="JsonSerializerOptions"/> used for serialization and deserialization.
        /// </summary>
        public abstract JsonSerializerOptions Options { get; }

        /// <inheritdoc />
        public SupportedDeserializationOptions SupportedDeserializationOptions => SupportedDeserializationOptionsStatic;

        /// <inheritdoc />
        public DeserializationOptions? DeserializationOptions { get; set; }

        #region Deserialization

        /// <inheritdoc />
        public abstract T? Deserialize<T>(ReadOnlyMemory<byte> buffer);

        /// <inheritdoc />
        public abstract T? Deserialize<T>(ReadOnlySequence<byte> buffer);

        /// <inheritdoc />
        public abstract T? Deserialize<T>(Stream stream);

        /// <inheritdoc />
        public abstract ValueTask<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default);

        #endregion

        #region Serialization

        /// <inheritdoc />
        /// <remarks>
        /// This overload does not make use of <see cref="JsonSerializerContext"/>.
        /// </remarks>
        public abstract void Serialize(Stream stream, object? obj);

        /// <inheritdoc />
        /// <remarks>
        /// This overload does not make use of <see cref="JsonSerializerContext"/>.
        /// </remarks>
        public abstract ValueTask SerializeAsync(Stream stream, object? obj,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Serializes the specified object onto a <see cref="Utf8JsonWriter"/>.
        /// </summary>
        /// <param name="stream">The writer to receive the serialized object.</param>
        /// <param name="obj">The object to serialize.</param>
        public abstract void Serialize(Utf8JsonWriter stream, object? obj);

        /// <summary>
        /// Serializes the specified object onto a stream.
        /// </summary>
        /// <typeparam name="T">Type of object to serialize.</typeparam>
        /// <param name="stream">The stream to receive the serialized object.</param>
        /// <param name="obj">The object to serialize.</param>
        public abstract void Serialize<T>(Stream stream, T obj);

        /// <inheritdoc />
        public abstract void Serialize<T>(IBufferWriter<byte> writer, T obj);

        /// <summary>
        /// Serializes the specified object onto a stream.
        /// </summary>
        /// <typeparam name="T">Type of object to serialize.</typeparam>
        /// <param name="stream">The stream to receive the serialized object.</param>
        /// <param name="obj">The object to serialize.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public abstract ValueTask SerializeAsync<T>(Stream stream, T obj, CancellationToken cancellationToken = default);

        /// <summary>
        /// Serializes the specified object onto a <see cref="Utf8JsonWriter"/>.
        /// </summary>
        /// <typeparam name="T">Type of object to serialize.</typeparam>
        /// <param name="stream">The writer to receive the serialized object.</param>
        /// <param name="obj">The object to serialize.</param>
        public abstract void Serialize<T>(Utf8JsonWriter stream, T obj);

        #endregion

        #region GetMemberName

        // Member name info is cached on a per-serializer basis, since it is affected by JsonSerializerOptions.PropertyNamingPolicy.
        // The cache is lazy-initialized to avoid heap allocations when it is unused.

        private ConcurrentDictionary<MemberInfo, string?>? _memberNameCache;
        private Func<MemberInfo, string?>? _getMemberNameAction;

        /// <inheritdoc />
        public string? GetMemberName(MemberInfo member)
        {
            var cache = Volatile.Read(ref _memberNameCache);
            if (cache is null)
            {
                // Make a new cache
                cache = new ConcurrentDictionary<MemberInfo, string?>();

                // Swap in only if null, return the original value if swap failed
                cache = Interlocked.CompareExchange(ref _memberNameCache, cache, null) ?? cache;
            }

            return cache.GetOrAdd(member, _getMemberNameAction ??= GetMemberNameCore);
        }

        private string? GetMemberNameCore(MemberInfo member)
        {
            if (member.GetCustomAttribute<JsonIgnoreAttribute>() is { Condition: JsonIgnoreCondition.Always })
            {
                return null;
            }

            var attr = member.GetCustomAttribute<JsonPropertyNameAttribute>();
            if (attr is not null)
            {
                return attr.Name;
            }

            return Options.PropertyNamingPolicy?.ConvertName(member.Name) ?? member.Name;
        }

        #endregion

        #region Creation

        /// <summary>
        /// Create a new SystemTextJsonSerializer with default options, optionally enabling increased Newtonsoft.Json compatibility.
        /// </summary>
        /// <param name="increasedNewtonsoftCompatibility">Enable increased Newtonsoft.Json compatibility.</param>
        /// <remarks>
        /// The <paramref name="increasedNewtonsoftCompatibility"/> parameter doesn't make this fully compatible with Newtonsoft.Json.
        /// However, it does enable several features to make it more compatible such as case-insensitive property name deserialization,
        /// ignoring comments and trailing commas, serializing public fields, etc. For details on compatibility, see
        /// https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-migrate-from-newtonsoft-how-to
        /// </remarks>
        /// <returns>A new SystemTextJsonSerializer.</returns>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationDynamicCodeMessage)]
        public static SystemTextJsonSerializer Create(bool increasedNewtonsoftCompatibility = false) =>
            Create(CreateDefaultOptions(increasedNewtonsoftCompatibility));

        /// <summary>
        /// Create a new SystemTextJsonSerializer with supplied <see cref="JsonSerializerOptions"/>.
        /// </summary>
        /// <param name="options"><see cref="JsonSerializerOptions"/> to control serialization and deserialization.</param>
        /// <returns>A new SystemTextJsonSerializer.</returns>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationDynamicCodeMessage)]
        public static SystemTextJsonSerializer Create(JsonSerializerOptions options)
        {
            if (options == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(options));
            }

            // Ensure that the options are read only and the TypeInfoResolver is populated in a thread-safe manner.
            // Typically, this is done internally by System.Text.Json on the first serialization or deserialization.
            // This ensures that JsonSerializerOptions.GetTypeInfo will work without requiring the options to be used
            // in a serialization or deserialization first.
            options.MakeReadOnly(populateMissingResolver: true);

            return new TypeInfoSystemTextJsonSerializer(options);
        }

        /// <summary>
        /// Create a new SystemTextJsonSerializer using a supplied <see cref="JsonSerializerContext"/>.
        /// </summary>
        /// <param name="context"><see cref="JsonSerializerOptions"/> to use for serialization and deserialization.</param>
        /// <remarks>
        /// This overload should be used to supply custom serializers on a per-request basis that are optimized for the particular
        /// type being serialized or deserialized. Any type which isn't registered in the <see cref="JsonSerializerContext"/>
        /// will be handled using the <see cref="JsonSerializerContext.Options"/>.
        /// </remarks>
        /// <returns>A new SystemTextJsonSerializer.</returns>
        public static SystemTextJsonSerializer Create(JsonSerializerContext context)
        {
            if (context == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(context));
            }

            return new TypeInfoSystemTextJsonSerializer(context.Options);
        }

        #endregion

        /// <inheritdoc />
        public abstract bool CanSerialize(Type type);

        /// <inheritdoc />
        public abstract IProjectionBuilder CreateProjectionBuilder(ILogger logger);

        /// <inheritdoc />
        public abstract IJsonStreamReader CreateJsonStreamReader(Stream stream);

        internal static JsonReaderOptions GetJsonReaderOptions(JsonSerializerOptions options) =>
            new()
            {
                AllowTrailingCommas = options.AllowTrailingCommas,
                CommentHandling = options.ReadCommentHandling,
                MaxDepth = GetEffectiveMaxDepth(options.MaxDepth),
            };

        internal static JsonWriterOptions GetJsonWriterOptions(JsonSerializerOptions options) =>
            new()
            {
                // TODO: Add indent controls when upgraded to STJ 9.0
                Encoder = options.Encoder,
                Indented = options.WriteIndented,
                MaxDepth = GetEffectiveMaxDepth(options.MaxDepth),
#if !DEBUG
                SkipValidation = true
#endif
            };

        private static int GetEffectiveMaxDepth(int maxDepth) =>
            // Emulates default behavior of JsonSerializerOptions.MaxDepth used internally by STJ
            maxDepth == 0 ? 64 : maxDepth;

        private static JsonSerializerOptions CreateDefaultOptions(bool increasedNewtonsoftCompatibility) =>
            new()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // This setting is always on by default
                AllowTrailingCommas = increasedNewtonsoftCompatibility,
                IncludeFields = increasedNewtonsoftCompatibility,
                NumberHandling = increasedNewtonsoftCompatibility ? JsonNumberHandling.AllowReadingFromString : JsonNumberHandling.Strict,
                PropertyNameCaseInsensitive = increasedNewtonsoftCompatibility,
                ReadCommentHandling = increasedNewtonsoftCompatibility ? JsonCommentHandling.Skip : JsonCommentHandling.Disallow
            };
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
