using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Couchbase.Query;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Management.Eventing.Internal
{
    /// <summary>
    /// This converter is used internally for converting <see cref="QueryScanConsistency"/> in cases where "none" is
    /// returned by the server and converting it to <see cref="QueryScanConsistency.NotBounded"/> in the SDK.
    /// </summary>
    internal class QueryScanConsistencyConverter : JsonConverter<QueryScanConsistency>
    {
        public override QueryScanConsistency Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String && reader.ValueTextEquals("none"))
            {
                return QueryScanConsistency.NotBounded;
            }

            return QueryScanConsistency.RequestPlus;
        }

        public override void Write(Utf8JsonWriter writer, QueryScanConsistency value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.GetDescription());
        }
    }
}
