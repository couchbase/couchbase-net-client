using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Couchbase.Core.Serialization
{
    /// <summary>
    /// <see cref="JsonConverter"/> that wraps an <see cref="ICustomObjectCreator"/> to support Json.Net deserialization.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="DefaultSerializer"/> if an <see cref="ICustomObjectCreator"/> is supplied.
    /// </remarks>
    internal class JsonNetCustomObjectCreatorWrapper : JsonConverter
    {
        private readonly ICustomObjectCreator _creator;

        public JsonNetCustomObjectCreatorWrapper(ICustomObjectCreator creator)
        {
            if (creator == null)
            {
                throw new ArgumentNullException("creator");
            }

            _creator = creator;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            object obj = _creator.CreateObject(objectType);
            if (obj == null)
            {
                throw new Exception("ICustomObjectCreator returned a null reference.");
            }

            serializer.Populate(reader, obj);

            return obj;
        }

        public override bool CanConvert(Type objectType)
        {
            return _creator.CanCreateObject(objectType);
        }
    }
}
