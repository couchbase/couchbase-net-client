using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

#nullable enable

namespace Couchbase.Core.IO.Serializers
{
    /// <summary>
    /// The default serializer for the Couchbase.NET SDK. Uses Newtonsoft.JSON as the the serializer.
    /// </summary>
    public class DefaultSerializer : IExtendedTypeSerializer, IStreamingTypeDeserializer, IProjectableTypeDeserializer, IBufferedTypeSerializer
    {
        public const string UnreferencedCodeMessage =
            "The DefaultSerializer uses Newtonsoft.Json which requires unreferenced code and is incompatible with trimming.";
        public const string RequiresDynamicCodeMessage =
            "The DefaultSerializer uses Newtonsoft.Json which requires dynamic code and is incompatible with AOT.";

        private static DefaultSerializer? _instance;
        internal static DefaultSerializer Instance
        {
            [RequiresUnreferencedCode(UnreferencedCodeMessage)]
            [RequiresDynamicCode(RequiresDynamicCodeMessage)]
            get
            {
                // First do a lock (and interlock) free check to see if the default serializer has been set.
                var instance = _instance;
                if (instance is not null)
                {
                    return instance;
                }

                // Not set yet, or very recently set by another thread, so set using Interlocked.CompareExchange to ensure only a single instance is ever returned.
                return Interlocked.CompareExchange(ref _instance, new DefaultSerializer(), null) ?? _instance;
            }
        }

        #region Constructors

        [RequiresUnreferencedCode(UnreferencedCodeMessage)]
        [RequiresDynamicCode(RequiresDynamicCodeMessage)]
        public DefaultSerializer() : this(
            new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                // https://issues.couchbase.com/browse/NCBC-3195
                // When deserializing of "SELECT RAW" queries of a plain DateTimeOffset, this setting
                // is required to avoid the loss of time zone data. This is due to the internals of the JsonTextReader
                // used in DefaultJsonStreamReader reading the value to a boxed DateTime before the target type
                // is provided in the call to ReadObjectAsync.
                DateParseHandling = DateParseHandling.DateTimeOffset
            },
            new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() })
        {
        }

        [RequiresUnreferencedCode(UnreferencedCodeMessage)]
        [RequiresDynamicCode(RequiresDynamicCodeMessage)]
        public DefaultSerializer(JsonSerializerSettings deserializationSettings, JsonSerializerSettings serializerSettings)
        {
            if (deserializationSettings == null)
            {
                throw new ArgumentNullException(nameof(deserializationSettings));
            }
            if (serializerSettings == null)
            {
                throw new ArgumentNullException(nameof(serializerSettings));
            }

            // If unassigned, set default ContractResolver for both DeserializationSettings and SerializerSettings
            deserializationSettings.ContractResolver ??= GetDefaultContractResolver();
            serializerSettings.ContractResolver ??= GetDefaultContractResolver();

            DeserializationSettings = deserializationSettings;
            SerializerSettings = serializerSettings;
        }

        [RequiresUnreferencedCode(UnreferencedCodeMessage)]
        [RequiresDynamicCode(RequiresDynamicCodeMessage)]
        private static IContractResolver GetDefaultContractResolver()
        {
            var defaultResolver = JsonConvert.DefaultSettings?.Invoke()?.ContractResolver;

            return defaultResolver ?? new DefaultContractResolver();
        }

        #endregion

        #region Fields

        private JsonSerializerSettings _serializationSettings = null!;
        private JsonSerializerSettings _deserializationSettings = null!;
        private DeserializationOptions? _deserializationOptions;

        private JsonSerializer _serializer = null!;
        private JsonSerializer _deserializer = null!;

        #endregion

        #region Properties

        private static readonly SupportedDeserializationOptions StaticSupportedDeserializationOptions =
            new SupportedDeserializationOptions()
            {
                CustomObjectCreator = true
            };

        /// <summary>
        /// Informs consumers what deserialization options this <see cref="IExtendedTypeSerializer"/> supports.
        /// </summary>
        public SupportedDeserializationOptions SupportedDeserializationOptions
        {
            get { return StaticSupportedDeserializationOptions; }
        }

        /// <summary>
        /// Gets the outgoing serializer settings; controls the format of the JSON you are storing in Couchbase.
        /// </summary>
        /// <value>
        /// The outgoing serializer settings; controls the format of the JSON you are storing in Couchbase.
        /// </value>
        public JsonSerializerSettings SerializerSettings
        {
            get => _serializationSettings;
            private set
            {
                _serializationSettings = value ?? throw new ArgumentNullException(nameof(value));

                _serializer = JsonSerializer.Create(SerializerSettings);
            }
        }

        /// <summary>
        /// Gets the incoming de-serializer settings; controls the format of the incoming JSON for de-serialization into POCOs.
        /// </summary>
        /// <value>
        /// The incoming serializer settings.
        /// </value>
        public JsonSerializerSettings DeserializationSettings {
            get => _deserializationSettings;
            [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
                Justification = "This type may not be constructed without encountering a warning.")]
            private set
            {
                _deserializationSettings = value ?? throw new ArgumentNullException(nameof(value));

                EffectiveDeserializationSettings = GetDeserializationSettings(_deserializationSettings,
                    _deserializationOptions);

                _deserializer = JsonSerializer.Create(EffectiveDeserializationSettings);
            }
        }

        /// <summary>
        /// Provides custom deserialization options.  Options not listed in <see cref="IExtendedTypeSerializer.SupportedDeserializationOptions"/>
        /// will be ignored.  If null, then defaults will be used.
        /// </summary>
        public DeserializationOptions? DeserializationOptions
        {
            get { return _deserializationOptions; }
            [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
                Justification = "This type may not be constructed without encountering a warning.")]
            set
            {
                _deserializationOptions = value;

                EffectiveDeserializationSettings = GetDeserializationSettings(_deserializationSettings,
                    _deserializationOptions);

                _deserializer = JsonSerializer.Create(EffectiveDeserializationSettings);
            }
        }

        internal JsonSerializerSettings EffectiveDeserializationSettings { get; set; } = null!;

        #endregion

        #region Methods

        /// <inheritdoc />
        [UnconditionalSuppressMessage("Aot", "IL3050",
            Justification = "This type may not be constructed without encountering a warning.")]
        public T? Deserialize<T>(ReadOnlyMemory<byte> buffer) =>
            Deserialize<T>(new ReadOnlySequence<byte>(buffer));

        /// <inheritdoc />
        [UnconditionalSuppressMessage("Aot", "IL3050",
            Justification = "This type may not be constructed without encountering a warning.")]
        public T? Deserialize<T>(ReadOnlySequence<byte> buffer)
        {
            var value = default(T);
            if (buffer.Length == 0) return value!;

            // handle binary-friendly types
            if (typeof(T) == typeof(byte[])) return (T)(object)buffer.ToArray();
            if (typeof(T) == typeof(ReadOnlyMemory<byte>)) return (T)(object) new ReadOnlyMemory<byte>(buffer.ToArray());
            if (typeof(T) == typeof(Memory<byte>)) return (T)(object)buffer.ToArray().AsMemory();
            if (typeof(T) == typeof(ReadOnlySequence<byte>)) return (T)(object)new ReadOnlySequence<byte>(buffer.ToArray());

            var reader = Utf8MemoryReader.InstancePool.Get();
            try
            {
                // Strip any leading BOM for backward compatibility
                reader.SetSequence(Utf8Helpers.TrimBomIfPresent(buffer));

                using var jr = new JsonTextReader(reader)
                {
                    CloseInput = false,
                    ArrayPool = JsonArrayPool.Instance
                };

                //use the following code block only for value types
                //strangely enough Nullable<T> itself is a value type so we need to filter it out
                if (typeof(T).IsValueType && (!typeof(T).IsGenericType ||
                                              typeof(T).GetGenericTypeDefinition() != typeof(Nullable<>)))
                {
                    //we can't declare Nullable<T> because T is not restricted to struct in this method scope
                    object? nullableVal = _deserializer.Deserialize(jr,
                        typeof(Nullable<>).MakeGenericType(typeof(T)));
                    //either we have a null or an instance of Nullable<T> that can be cast directly to T
                    value = nullableVal == null ? default(T)! : (T)nullableVal;
                }
                else
                {
                    value = _deserializer.Deserialize<T>(jr);
                }

                return value;
            }
            finally
            {
                Utf8MemoryReader.InstancePool.Return(reader);
            }
        }

        /// <inheritdoc />
        public ValueTask<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
        {
            return new ValueTask<T?>(Deserialize<T>(stream)!);
        }

        /// <inheritdoc />
        public void Serialize(Stream stream, object? obj)
        {
            using (var sw = new StreamWriter(stream, EncodingUtils.Utf8NoBomEncoding, 1024, true))
            {
                using (var jr = new JsonTextWriter(sw)
                {
                    CloseOutput = false,
                    ArrayPool = JsonArrayPool.Instance
                })
                {
                    _serializer.Serialize(jr, obj);
                }
            }
        }

        /// <inheritdoc />
        public void Serialize<T>(IBufferWriter<byte> writer, T obj)
        {
            using var stream = new BufferWriterStream(writer);

            Serialize(stream, obj);
        }

        /// <inheritdoc />
        public ValueTask SerializeAsync(Stream stream, object? obj, CancellationToken cancellationToken = default)
        {
            Serialize(stream, obj);

            return default;
        }

        /// <inheritdoc />
        public T? Deserialize<T>(Stream stream)
        {
            using (var streamReader = new StreamReader(stream))
            {
                using (var reader = new JsonTextReader(streamReader)
                {
                    ArrayPool = JsonArrayPool.Instance
                })
                {
                    return _deserializer.Deserialize<T>(reader);
                }
            }
        }

        /// <inheritdoc />
        public bool CanSerialize(Type type) => true;

        /// <inheritdoc />
        /// <remarks>
        /// DefaultSerializer uses <see cref="JsonSerializerSettings.ContractResolver"/> from <see cref="SerializerSettings"/>
        /// to determine the member name.
        /// </remarks>
        public string? GetMemberName(MemberInfo member)
        {
            if (member == null)
            {
                throw new ArgumentNullException("member");
            }

            var contract = SerializerSettings.ContractResolver?.ResolveContract(member.DeclaringType!) as JsonObjectContract;

            if (contract != null)
            {
                var property = contract.Properties.FirstOrDefault(
                    p => p.UnderlyingName == member.Name && !p.Ignored);

                if (property != null)
                {
                    return property.PropertyName;
                }
            }

            // No match found, or property is ignored
            return null;
        }

        /// <inheritdoc />
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
            Justification = "This type may not be constructed without encountering a warning.")]
        [UnconditionalSuppressMessage("Aot", "IL3050",
            Justification = "This type may not be constructed without encountering a warning.")]
        public IJsonStreamReader CreateJsonStreamReader(Stream stream)
        {
            return new DefaultJsonStreamReader(stream, _deserializer);
        }

        /// <inheritdoc />
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
            Justification = "This type may not be constructed without encountering a warning.")]
        [UnconditionalSuppressMessage("Aot", "IL3050",
            Justification = "This type may not be constructed without encountering a warning.")]
        public IProjectionBuilder CreateProjectionBuilder(ILogger logger) => new NewtonsoftProjectionBuilder(this, logger);

        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
            Justification = "This type may not be constructed without encountering a warning.")]
        protected internal virtual JsonSerializerSettings GetDeserializationSettings(JsonSerializerSettings baseSettings, DeserializationOptions? options)
        {
            if ((options == null) || !options.HasSettings)
            {
                // No custom deserialization, so use baseSettings directly

                return baseSettings;
            }

            var settings = new JsonSerializerSettings()
            {
                CheckAdditionalContent = baseSettings.CheckAdditionalContent,
                ConstructorHandling = baseSettings.ConstructorHandling,
                Context = baseSettings.Context,
                ContractResolver = baseSettings.ContractResolver,
                Converters = new List<JsonConverter>(baseSettings.Converters),
                Culture = baseSettings.Culture,
                DateFormatHandling = baseSettings.DateFormatHandling,
                DateFormatString = baseSettings.DateFormatString,
                DateParseHandling = baseSettings.DateParseHandling,
                DateTimeZoneHandling = baseSettings.DateTimeZoneHandling,
                DefaultValueHandling = baseSettings.DefaultValueHandling,
                FloatFormatHandling = baseSettings.FloatFormatHandling,
                FloatParseHandling = baseSettings.FloatParseHandling,
                Formatting = baseSettings.Formatting,
                MaxDepth = baseSettings.MaxDepth,
                NullValueHandling = baseSettings.NullValueHandling,
                ObjectCreationHandling = baseSettings.ObjectCreationHandling,
                PreserveReferencesHandling = baseSettings.PreserveReferencesHandling,
                ReferenceLoopHandling = baseSettings.ReferenceLoopHandling,
                ReferenceResolverProvider = baseSettings.ReferenceResolverProvider,
                StringEscapeHandling = baseSettings.StringEscapeHandling,
                TraceWriter = baseSettings.TraceWriter,
                TypeNameHandling = baseSettings.TypeNameHandling,
            };

            if (options.CustomObjectCreator != null)
            {
                settings.Converters.Add(new JsonNetCustomObjectCreatorWrapper(options.CustomObjectCreator));
            }

            return settings;
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
