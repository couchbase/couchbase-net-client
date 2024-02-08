#if NET5_0_OR_GREATER
#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Couchbase.Integrated.Transactions.DataModel
{
    internal record QueryTxData(
        CompositeId id,
        TxDataState state,
        TxDataReportedConfig config,
        AtrRef? atr,
        IEnumerable<TxDataMutation> mutations)
    {
        public JObject ToJson()
        {
            var jobj = JObject.FromObject(this);
            if (atr == null)
            {
                jobj.Remove("atr");
            }

            return jobj;
        }
    }

    internal record TxDataState(long timeLeftMs);

    // NOTE: numAtrs should be removed, but that would be incompatible with server version 7.0.0
    internal record TxDataReportedConfig(long kvTimeoutMs, int numAtrs, string durabilityLevel);
    internal record TxDataMutation(string scp, string coll, string bkt, string id, string cas, string type);
}
#endif
