using System;
using Newtonsoft.Json;

namespace Couchbase.Management.Eventing.Internal
{
    internal class EventingFunctionProcessingStatusConverter : JsonConverter<EventingFunctionProcessingStatus>
    {
        public override void WriteJson(JsonWriter writer, EventingFunctionProcessingStatus value, JsonSerializer serializer)
        {
            //This JSON is read only
        }

        public override EventingFunctionProcessingStatus ReadJson(JsonReader reader, Type objectType,
            EventingFunctionProcessingStatus existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var value = Convert.ToBoolean(reader.Value);
            return value
                    ? EventingFunctionProcessingStatus.Running
                    : EventingFunctionProcessingStatus.Paused;
        }
    }
}
