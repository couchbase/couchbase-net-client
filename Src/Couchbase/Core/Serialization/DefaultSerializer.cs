using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Couchbase.Core.Serialization
{
    /// <summary>
    /// The default serializer for the Couchbase.NET SDK. Uses Newtonsoft.JSON as the the serializer.
    /// </summary>
    public class DefaultSerializer : IExtendedTypeSerializer
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

        /// <summary>
        /// Deserializes the specified buffer into the <see cref="Type"/> T specified as a generic parameter.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> specified as the type of the value.</typeparam>
        /// <param name="buffer">The buffer to deserialize from.</param>
        /// <param name="offset">The offset of the buffer to start reading from.</param>
        /// <param name="length">The length of the buffer to read from.</param>
        /// <returns>The <see cref="Type"/> instance representing the value of the key.</returns>
        public T Deserialize<T>(byte[] buffer, int offset, int length)
        {
            T value = default(T);
            if (length == 0) return value;
            using (var ms = new MemoryStream(buffer, offset, length))
            {
                using (var sr = new StreamReader(ms))
                {
                    using (var jr = new JsonTextReader(sr))
                    {
                        var serializer = JsonSerializer.Create(EffectiveDeserializationSettings);

                        //use the following code block only for value types
                        //strangely enough Nullable<T> itself is a value type so we need to filter it out
                        if (typeof(T).IsValueType && (!typeof(T).IsGenericType || typeof(T).GetGenericTypeDefinition() != typeof(Nullable<>)))
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

        /// <summary>
        /// Serializes the specified object into a buffer.
        /// </summary>
        /// <param name="obj">The object to serialize.</param>
        /// <returns>A <see cref="byte"/> array that is the serialized value of the key.</returns>
        public byte[] Serialize(object obj)
        {
            using (var ms = new MemoryStream())
            {
                using (var sw = new StreamWriter(ms))
                {
                    using (var jr = new JsonTextWriter(sw))
                    {
                        var serializer = JsonSerializer.Create(SerializerSettings);
                        serializer.Serialize(jr, obj);
                    }
                }
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Deserializes the specified stream into the <see cref="Type"/> T specified as a generic parameter.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> specified as the type of the value.</typeparam>
        /// <param name="stream">The stream.</param>
        /// <returns>The <see cref="Type"/> instance representing the value of the key.</returns>
        public T Deserialize<T>(Stream stream)
        {
            using (var streamReader = new StreamReader(stream))
            {
                using (var reader = new JsonTextReader(streamReader))
                {
                    var serializer = JsonSerializer.Create(EffectiveDeserializationSettings);
                    return serializer.Deserialize<T>(reader);
                }
            }
        }

        /// <summary>
        /// Get the name which will be used for a given member during serialization/deserialization.
        /// </summary>
        /// <param name="member">Returns the name of this member.</param>
        /// <returns>
        /// The name which will be used for a given member during serialization/deserialization,
        /// or null if if will not be serialized.
        /// </returns>
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

        protected internal virtual JsonSerializerSettings GetDeserializationSettings(JsonSerializerSettings baseSettings, DeserializationOptions options)
        {
            if ((options == null) || !options.HasSettings)
            {
                // No custom deserialization, so use baseSettings directly

                return baseSettings;
            }

            var settings = new JsonSerializerSettings()
            {
                Binder = baseSettings.Binder,
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
                TypeNameAssemblyFormat = baseSettings.TypeNameAssemblyFormat,
                TypeNameHandling = baseSettings.TypeNameHandling
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
