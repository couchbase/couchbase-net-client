using System;
using Couchbase.Core.Exceptions;
using Newtonsoft.Json;

namespace Couchbase.Management.Eventing.Internal
{
    internal class EventingFunctionStatusConverter : JsonConverter<EventingFunctionStatus>
    {
        public override void WriteJson(JsonWriter writer, EventingFunctionStatus value, JsonSerializer serializer)
        {
            //This JSON is read only
        }

        public override EventingFunctionStatus ReadJson(JsonReader reader, Type objectType,
            EventingFunctionStatus existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            return reader.Value.ToString() switch
            {
                "deployed" => EventingFunctionStatus.Deployed,
                "deploying" => EventingFunctionStatus.Deploying,
                "paused" => EventingFunctionStatus.Paused,
                "pausing" => EventingFunctionStatus.Pausing,
                "undeploying" => EventingFunctionStatus.UnDeploying,
                "undeployed" => EventingFunctionStatus.Undeployed,
                _ => throw new InvalidArgumentException(
                    "The EventingFunctionStatus value returned by the server is not supported by the client.")
            };
        }
    }
}
