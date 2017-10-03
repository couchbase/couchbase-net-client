using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    public class Services
    {
        [JsonProperty("fts")]
        public int Fts { get; set; }

        [JsonProperty("mgmt")]
        public int Mgmt { get; set; }

        [JsonProperty("moxi")]
        public int Moxi { get; set; }

        [JsonProperty("kv")]
        public int KV { get; set; }

        [JsonProperty("capi")]
        public int Capi { get; set; }

        [JsonProperty("kvSSL")]
        public int KvSSL { get; set; }

        [JsonProperty("capiSSL")]
        public int CapiSSL { get; set; }

        [JsonProperty("mgmtSSL")]
        public int MgmtSSL { get; set; }

        [JsonProperty("projector")]
        public int Projector { get; set; }

        [JsonProperty("indexAdmin")]
        public int IndexAdmin { get; set; }

        [JsonProperty("indexScan")]
        public int IndexScan { get; set; }

        [JsonProperty("indexHttp")]
        public int IndexHttp { get; set; }

        [JsonProperty("indexStreamInit")]
        public int IndexStreamInit { get; set; }

        [JsonProperty("indexStreamCatchup")]
        public int IndexStreamCatchup { get; set; }

        [JsonProperty("indexStreamMaint")]
        public int IndexStreamMaint { get; set; }

        [JsonProperty("n1ql")]
        // ReSharper disable once InconsistentNaming
        public int N1QL { get; set; }

        [JsonProperty("n1qlSSL")]
        // ReSharper disable once InconsistentNaming
        public int N1QLSsl { get; set; }

        [JsonProperty("cbas")]
        public int Analytics { get; set; }

        [JsonProperty("cbasSSL")]
        public int AnalyticsSsl { get; set; }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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

#endregion
