#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Couchbase.Client.Transactions.DataModel
{
    internal record QueryGetResult(string scas, object doc, object? txnMeta)
    {
        [JsonExtensionData]
        IDictionary<string, object>? extras { get; set; }
    }
    internal record QueryInsertResult(string scas);
}
