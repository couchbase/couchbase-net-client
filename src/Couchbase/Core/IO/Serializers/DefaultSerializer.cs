using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Couchbase.Core.IO.Serializers
{
     /// <summary>
    /// The default serializer for the Couchbase.NET SDK. Uses Newtonsoft.JSON as the the serializer.
    /// </summary>
    public class DefaultSerializer : IExtendedTypeSerializer, IStreamingTypeDeserializer
    {
        #region Constructors

        public DefaultSerializer() : this(
            new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() },
            new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() })
        {
        }

        public DefaultSerializer(JsonSerializerSettings deserializationSettings, JsonSerializerSettings serializerSettings)
        {
            DeserializationSettings = deserializationSettings;
            SerializerSettings = serializerSettings;

            // If unassigned, set default ContractResovler for both DeserializationSettings and SerializerSettingssettings
            if (DeserializationSettings.ContractResolver == null)
            {
                DeserializationSettings.ContractResolver = GetDefaultContractResolver();
            }
            if (SerializerSettings.ContractResolver == null)
            {
                SerializerSettings.ContractResolver = GetDefaultContractResolver();
            }
        }

        private static IContractResolver GetDefaultContractResolver()
        {
            // Would be nice to use null propagation here (requires C# 6)
            if (JsonConvert.DefaultSettings != null &&
                JsonConvert.DefaultSettings() != null &&
                JsonConvert.DefaultSettings().ContractResolver != null)
            {
                return JsonConvert.DefaultSettings().ContractResolver;
            }

            return new DefaultContractResolver();
        }

        #endregion

        #region Fields

        private static readonly Encoding Utf8NoBomEncoding = new UTF8Encoding(false);

        private JsonSerializerSettings _deserializationSettings;
        private DeserializationOptions _deserializationOptions;

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
        public JsonSerializerSettings SerializerSettings { get; private set; }

        /// <summary>
        /// Gets the incoming de-serializer settings; controls the format of the incoming JSON for de-serialization into POCOs.
        /// </summary>
        /// <value>
        /// The incoming serializer settings.
        /// </value>
        public JsonSerializerSettings DeserializationSettings {
            get { return _deserializationSettings; }
            private set
            {
                _deserializationSettings = value;

                EffectiveDeserializationSettings = GetDeserializationSettings(_deserializationSettings,
                    _deserializationOptions);
            }
        }

        /// <summary>
        /// Provides custom deserialization options.  Options not listed in <see cref="IExtendedTypeSerializer.SupportedDeserializationOptions"/>
        /// will be ignored.  If null, then defaults will be used.
        /// </summary>
        public DeserializationOptions DeserializationOptions
        {
            get { return _deserializationOptions; }
            set
            {
                _deserializationOptions = value;

                EffectiveDeserializationSettings = GetDeserializationSettings(_deserializationSettings,
                    _deserializationOptions);
            }
        }

        internal JsonSerializerSettings EffectiveDeserializationSettings { get; set; }

        #endregion

        #region Methods

        /// <inheritdoc />
        public T Deserialize<T>(ReadOnlyMemory<byte> buffer)
        {
            T value = default(T);
            if (buffer.Length == 0) return value;
            using (var ms = new MemoryReaderStream(buffer))
            {
                using (var sr = new StreamReader(ms))
                {
                    using (var jr = new JsonTextReader(sr)
                    {
                        ArrayPool = JsonArrayPool.Instance
                    })
                    {
                        var serializer = JsonSerializer.Create(EffectiveDeserializationSettings);

                        //use the following code block only for value types
                        //strangely enough Nullable<T> itself is a value type so we need to filter it out
                        if (typeof(T).GetTypeInfo().IsValueType && (!typeof(T).GetTypeInfo().IsGenericType || typeof(T).GetGenericTypeDefinition() != typeof(Nullable<>)))
                        {
                            //we can't declare Nullable<T> because T is not restricted to struct in this method scope
                            object nullableVal = serializer.Deserialize(jr, typeof(Nullable<>).MakeGenericType(typeof(T)));
                            //either we have a null or an instance of Nullabte<T> that can be cast directly to T
                            value = nullableVal == null ? default(T) : (T)nullableVal;
                        }
                        else
                        {
                            value = serializer.Deserialize<T>(jr);
                        }
                    }
                }
            }
            return value;
        }

        /// <inheritdoc />
        public ValueTask<T> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
        {
            return new ValueTask<T>(Deserialize<T>(stream));
        }

        /// <inheritdoc />
        public void Serialize(Stream stream, object obj)
        {
            using (var sw = new StreamWriter(stream, Utf8NoBomEncoding, 1024, true))
            {
                using (var jr = new JsonTextWriter(sw)
                {
                    CloseOutput = false,
                    ArrayPool = JsonArrayPool.Instance
                })
                {
                    var serializer = JsonSerializer.Create(SerializerSettings);
                    serializer.Serialize(jr, obj);
                }
            }
        }

        /// <inheritdoc />
        public ValueTask SerializeAsync(Stream stream, object obj, CancellationToken cancellationToken = default)
        {
            Serialize(stream, obj);

            return default;
        }

        /// <inheritdoc />
        public T Deserialize<T>(Stream stream)
        {
            using (var streamReader = new StreamReader(stream))
            {
                using (var reader = new JsonTextReader(streamReader)
                {
                    ArrayPool = JsonArrayPool.Instance
                })
                {
                    var serializer = JsonSerializer.Create(EffectiveDeserializationSettings);
                    return serializer.Deserialize<T>(reader);
                }
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// DefaultSerializer uses <see cref="JsonSerializerSettings.ContractResolver"/> from <see cref="SerializerSettings"/>
        /// to determine the member name.
        /// </remarks>
        public string GetMemberName(MemberInfo member)
        {
            if (member == null)
            {
                throw new ArgumentNullException("member");
            }

            var contract = SerializerSettings.ContractResolver.ResolveContract(member.DeclaringType) as JsonObjectContract;

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
        public IJsonStreamReader CreateJsonStreamReader(Stream stream)
        {
            return new DefaultJsonStreamReader(stream, JsonSerializer.Create(EffectiveDeserializationSettings));
        }

        protected internal virtual JsonSerializerSettings GetDeserializationSettings(JsonSerializerSettings baseSettings, DeserializationOptions options)
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
                StringEscapeHandling = baseSettings.StringEscapeHandling,
                TraceWriter = baseSettings.TraceWriter,
                TypeNameHandling = baseSettings.TypeNameHandling,
#if NET45
                // There is an incompatibility between the .Net Desktop and .Net Standard assembilies of Newtonsoft.Json
                // Which causes these settings to fail if the .Net Standard version of Couchbase is consumed by a .Net Desktop
                // assembly.  As a workaround, don't try to copy them in the .Net Standard version.

                Binder = baseSettings.Binder,
                TypeNameAssemblyFormat = baseSettings.TypeNameAssemblyFormat,
#endif
            };

#pragma warning disable 618
            if (baseSettings.ReferenceResolver != null)
#pragma warning restore 618
            {
                // Backwards compatibility issue in Newtonsoft.Json 7.0.1 causes setting a null reference resolver to error instead of using default
#pragma warning disable 618
                settings.ReferenceResolver = baseSettings.ReferenceResolver;
#pragma warning restore 618
            }

            if (options.CustomObjectCreator != null)
            {
                settings.Converters.Add(new JsonNetCustomObjectCreatorWrapper(options.CustomObjectCreator));
            }

            return settings;
        }

#endregion
    }

}
