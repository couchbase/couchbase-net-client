using System;
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

#nullable enable

// ReSharper disable once CheckNamespace
namespace Couchbase.Core.IO.Serializers
{
    /// <summary>
    /// A JSON serializer based on System.Text.Json.
    /// </summary>
    /// <remarks>
    /// This class is currently experimental and subject to change. It does not support all serialization features
    /// supported by the <see cref="DefaultSerializer"/>, such as <c>dynamic</c> support, streaming query results,
    /// or projections.
    /// </remarks>
    [InterfaceStability(Level.Volatile)]
    public abstract class SystemTextJsonSerializer : IExtendedTypeSerializer
    {
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
        /// Serializes the specified object onto a stream.
        /// </summary>
        /// <typeparam name="T">Type of object to serialize.</typeparam>
        /// <param name="stream">The stream to receive the serialized object.</param>
        /// <param name="obj">The object to serialize.</param>
        public abstract void Serialize<T>(Stream stream, T obj);

        /// <summary>
        /// Serializes the specified object onto a stream.
        /// </summary>
        /// <typeparam name="T">Type of object to serialize.</typeparam>
        /// <param name="stream">The stream to receive the serialized object.</param>
        /// <param name="obj">The object to serialize.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public abstract ValueTask SerializeAsync<T>(Stream stream, T obj, CancellationToken cancellationToken = default);

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
        [RequiresUnreferencedCode(ReflectionSystemTextJsonSerializer.SerializationUnreferencedCodeMessage)]
        public static SystemTextJsonSerializer Create(bool increasedNewtonsoftCompatibility = false) =>
            new ReflectionSystemTextJsonSerializer(increasedNewtonsoftCompatibility);

        /// <summary>
        /// Create a new SystemTextJsonSerializer with supplied <see cref="JsonSerializerOptions"/>.
        /// </summary>
        /// <param name="options"><see cref="JsonSerializerOptions"/> to control serialization and deserialization.</param>
        /// <returns>A new SystemTextJsonSerializer.</returns>
        [RequiresUnreferencedCode(ReflectionSystemTextJsonSerializer.SerializationUnreferencedCodeMessage)]
        public static SystemTextJsonSerializer Create(JsonSerializerOptions options) =>
            new ReflectionSystemTextJsonSerializer(options);

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
        public static SystemTextJsonSerializer Create(JsonSerializerContext context) =>
            new ContextSystemTextJsonSerializer(context);

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
