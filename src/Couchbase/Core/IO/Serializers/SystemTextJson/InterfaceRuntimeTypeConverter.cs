using System;
using System.Text.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Couchbase.Core.IO.Serializers.SystemTextJson;

internal class InterfaceRuntimeTypeConverter<TInterface> : System.Text.Json.Serialization.JsonConverter<TInterface>
{
    // Allows us to call JsonSerializer.Serialize on classes that implement TInterface and serialize
    // the actual runtime type and not just the stuff in the interface.
    public override void Write(Utf8JsonWriter writer, TInterface value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value?.GetType() ?? typeof(object), options);
    }
    public override TInterface Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        throw new NotImplementedException("Deserialization not supported.");
    }
}
