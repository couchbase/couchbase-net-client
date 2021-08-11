using System;
using System.Diagnostics;
using Newtonsoft.Json;

namespace Couchbase.Management.Eventing.Internal
{
    internal class EventingFunctionDeploymentStatusConverter : JsonConverter<EventingFunctionDeploymentStatus>
    {
        public override void WriteJson(JsonWriter writer, EventingFunctionDeploymentStatus value,
            JsonSerializer serializer)
        {
            //This JSON is read only
        }

        public override EventingFunctionDeploymentStatus ReadJson(JsonReader reader, Type objectType,
            EventingFunctionDeploymentStatus existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var value = Convert.ToBoolean(reader.Value);
            return value
                    ? EventingFunctionDeploymentStatus.Deployed
                    : EventingFunctionDeploymentStatus.Undeployed;
        }
    }
}
