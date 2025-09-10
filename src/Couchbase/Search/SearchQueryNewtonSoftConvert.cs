#nullable enable
using System;
using System.Text.Json;
using Newtonsoft.Json;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace Couchbase.Search.Serialization;

internal sealed class SearchQueryNewtonsoftConverter : JsonConverter<ISearchQuery>
{
    public override void WriteJson(JsonWriter writer, ISearchQuery? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        var jObj = value.Export();
        jObj.WriteTo(writer);
    }

    public override ISearchQuery? ReadJson(JsonReader reader, Type objectType, ISearchQuery? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        throw new NotSupportedException("Deserializing ISearchQuery from JSON is not supported.");
    }
}

internal sealed class SearchQuerySystemTextJsonConverter : System.Text.Json.Serialization.JsonConverter<ISearchQuery>
{
    public override ISearchQuery? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Deserializing ISearchQuery from JSON is not supported.");
    }

    public override void Write(Utf8JsonWriter writer, ISearchQuery value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        var jObj = value.Export();
        using var doc = JsonDocument.Parse(jObj.ToString());
        doc.RootElement.WriteTo(writer);
    }
}
