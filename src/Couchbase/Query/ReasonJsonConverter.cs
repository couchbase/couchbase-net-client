using System;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable

namespace Couchbase.Query;

// Query sends "reason" as a string or as an object. A nested Query error names its message "msg".
internal sealed class ReasonJsonConverter : JsonConverter<Reason>
{
    public override Reason? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new Reason { Message = reader.GetString() };
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return null;
        }

        var reason = new Reason();
        string? msg = null;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var name = reader.GetString();
            reader.Read();
            switch (name)
            {
                case "caller":
                    reason.Caller = ReadString(ref reader);
                    break;
                case "code" when reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var code):
                    reason.Code = code;
                    break;
                case "key":
                    reason.Key = ReadString(ref reader);
                    break;
                case "message":
                    reason.Message = ReadString(ref reader);
                    break;
                case "msg":
                    msg = ReadString(ref reader);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        reason.Message ??= msg;
        return reason;
    }

    public override void Write(Utf8JsonWriter writer, Reason value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("caller", value.Caller);
        writer.WriteNumber("code", value.Code);
        writer.WriteString("key", value.Key);
        writer.WriteString("message", value.Message);
        writer.WriteEndObject();
    }

    private static string? ReadString(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }

        reader.Skip();
        return null;
    }
}

// The Newtonsoft.Json twin of ReasonJsonConverter. Writing keeps the default object shape.
internal sealed class ReasonNewtonsoftConverter : Newtonsoft.Json.JsonConverter
{
    public override bool CanWrite => false;

    public override bool CanConvert(Type objectType) => objectType == typeof(Reason);

    public override object? ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, object? existingValue,
        Newtonsoft.Json.JsonSerializer serializer)
    {
        switch (reader.TokenType)
        {
            case Newtonsoft.Json.JsonToken.Null:
                return null;
            case Newtonsoft.Json.JsonToken.String:
                return new Reason { Message = (string?)reader.Value };
            case Newtonsoft.Json.JsonToken.StartObject:
                break;
            default:
                reader.Skip();
                return null;
        }

        var reason = new Reason();
        string? msg = null;
        while (reader.Read() && reader.TokenType == Newtonsoft.Json.JsonToken.PropertyName)
        {
            var name = (string?)reader.Value;
            reader.Read();
            switch (name)
            {
                case "caller":
                    reason.Caller = ReadString(reader);
                    break;
                case "code" when reader.Value is long code and >= int.MinValue and <= int.MaxValue:
                    reason.Code = (int)code;
                    break;
                case "key":
                    reason.Key = ReadString(reader);
                    break;
                case "message":
                    reason.Message = ReadString(reader);
                    break;
                case "msg":
                    msg = ReadString(reader);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        reason.Message ??= msg;
        return reason;
    }

    public override void WriteJson(Newtonsoft.Json.JsonWriter writer, object? value,
        Newtonsoft.Json.JsonSerializer serializer) =>
        throw new NotSupportedException();

    private static string? ReadString(Newtonsoft.Json.JsonReader reader)
    {
        if (reader.TokenType == Newtonsoft.Json.JsonToken.String)
        {
            return (string?)reader.Value;
        }

        reader.Skip();
        return null;
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2026 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
