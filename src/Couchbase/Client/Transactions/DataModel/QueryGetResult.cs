#nullable enable
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Couchbase.Client.Transactions.DataModel
{
    internal record QueryGetResult(string scas, object doc, object? txnMeta)
    {
        [JsonExtensionData]
        IDictionary<string, JsonElement>? extras { get; set; }
    }
    internal record QueryInsertResult(string scas);
}
