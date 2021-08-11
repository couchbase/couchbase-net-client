using System;
using Couchbase.Core.Exceptions;
using Newtonsoft.Json;

namespace Couchbase.Management.Eventing.Internal
{
    internal class LogLevelConverter : JsonConverter<EventingFunctionLogLevel>
    {
        public override void WriteJson(JsonWriter writer, EventingFunctionLogLevel value, JsonSerializer serializer)
        {
           //read-only
        }

        public override EventingFunctionLogLevel ReadJson(JsonReader reader, Type objectType, EventingFunctionLogLevel existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            return reader.Value.ToString() switch
            {
                "INFO" => EventingFunctionLogLevel.Info,
                "ERROR" => EventingFunctionLogLevel.Error,
                "WARNING" => EventingFunctionLogLevel.Warning,
                "DEBUG" => EventingFunctionLogLevel.Debug,
                "TRACE" => EventingFunctionLogLevel.Trace,
                _ => throw new InvalidArgumentException(
                    "The EventingFunctionLogLevel value returned by the server is not supported by the client.")
            };
        }
    }
}
