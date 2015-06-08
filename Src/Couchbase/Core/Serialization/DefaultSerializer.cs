using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Couchbase.Core.Serialization
{
    /// <summary>
    /// The default serializer for the Couchbase.NET SDK. Uses Newtonsoft.JSON as the the serializer.
    /// </summary>
    public class DefaultSerializer : ITypeSerializer
    {
        public DefaultSerializer() : this(
            new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() },
            new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() })
        {
        }

        public DefaultSerializer(JsonSerializerSettings deserializationSettings, JsonSerializerSettings serializerSettings)
        {
            DeserializationSettings = deserializationSettings;
            SerializerSettings = serializerSettings;
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
        public JsonSerializerSettings DeserializationSettings { get; private set; }


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
            T value = default (T);
            if (length == 0) return value;
            using (var ms = new MemoryStream(buffer, offset, length))
            {
                using (var sr = new StreamReader(ms))
                {
                    using (var jr = new JsonTextReader(sr))
                    {
                        var serializer = JsonSerializer.Create(DeserializationSettings);

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
                return JsonConvert.DeserializeObject<T>(streamReader.ReadToEnd(), DeserializationSettings);
            }
        }
    }
}
