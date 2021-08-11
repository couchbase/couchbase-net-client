using System;
using Newtonsoft.Json;

namespace Couchbase.Management.Eventing.Internal
{
    /// <summary>
    /// This converter is used internally for mapping <see cref="EventingFunctionBucketAccess"/>
    /// </summary>
    internal class BucketAccessConverter : JsonConverter<EventingFunctionBucketAccess>
    {
        public override void WriteJson(JsonWriter writer, EventingFunctionBucketAccess value, JsonSerializer serializer)
        {
            //This JSON is read only
        }

        public override EventingFunctionBucketAccess ReadJson(JsonReader reader, Type objectType, EventingFunctionBucketAccess existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.Value.ToString() == "r")
            {
                return EventingFunctionBucketAccess.ReadOnly;
            }

            return EventingFunctionBucketAccess.ReadWrite;
        }
    }
}
