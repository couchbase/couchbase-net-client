using System;
using Couchbase.Query;
using Newtonsoft.Json;

namespace Couchbase.Management.Eventing.Internal
{
    /// <summary>
    /// This converter is used internally for converting <see cref="QueryScanConsistency"/> in cases where "none" is
    /// returned by the server and converting it to <see cref="QueryScanConsistency.NotBounded"/> in the SDK.
    /// </summary>
    internal class QueryScanConsistencyConverter : JsonConverter<QueryScanConsistency>
    {
        public override void WriteJson(JsonWriter writer, QueryScanConsistency value, JsonSerializer serializer)
        {
            //This JSON is read only
        }

        public override QueryScanConsistency ReadJson(JsonReader reader, Type objectType, QueryScanConsistency existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.Value.ToString() == "none")
            {
                return QueryScanConsistency.NotBounded;
            }

            return QueryScanConsistency.RequestPlus;
        }
    }
}
