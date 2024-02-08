#if NET5_0_OR_GREATER
#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Couchbase.Integrated.Transactions.DataModel
{
    internal record QueryGetResult(string scas, object doc, JObject? txnMeta)
    {
        [JsonExtensionData]
        IDictionary<string, object>? extras { get; set; }
    }
    internal record QueryInsertResult(string scas);
}
#endif
