using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Couchbase.Transactions.DataModel
{
    internal record QueryGetResult(string scas, object doc, JObject? txnMeta)
    {
        [JsonExtensionData]
        IDictionary<string, object>? extras { get; set; }
    }
    internal record QueryInsertResult(string scas);
}
