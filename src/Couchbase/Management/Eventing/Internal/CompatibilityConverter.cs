using System;
using Couchbase.Core.Exceptions;
using Newtonsoft.Json;

namespace Couchbase.Management.Eventing.Internal
{
    internal class CompatibilityConverter : JsonConverter<EventingFunctionLanguageCompatibility>
    {
        public override void WriteJson(JsonWriter writer, EventingFunctionLanguageCompatibility value, JsonSerializer serializer)
        {
            //This JSON is read only
        }

        public override EventingFunctionLanguageCompatibility ReadJson(JsonReader reader, Type objectType,
            EventingFunctionLanguageCompatibility existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            return reader.Value.ToString() switch
            {
                "6.0.0" => EventingFunctionLanguageCompatibility.Version_6_0_0,
                "6.5.0" => EventingFunctionLanguageCompatibility.Version_6_5_0,
                "6.6.2" => EventingFunctionLanguageCompatibility.Version_6_6_2,
                _ => throw new InvalidArgumentException(
                    "The EventingFunctionLanguageCompatibility value returned by the server is not supported by the client.")
            };
        }
    }
}
