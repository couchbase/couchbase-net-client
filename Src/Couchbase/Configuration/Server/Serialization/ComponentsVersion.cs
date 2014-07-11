using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    internal sealed class ComponentsVersion
    {
        [JsonProperty("public_key")]
        public string PublicKey { get; set; }

        [JsonProperty("lhttpc")]
        public string Lhttpc { get; set; }

        [JsonProperty("ale")]
        public string Ale { get; set; }

        [JsonProperty("os_mon")]
        public string OsMon { get; set; }

        [JsonProperty("couch_set_view")]
        public string CouchSetView { get; set; }

        [JsonProperty("compiler")]
        public string Compiler { get; set; }

        [JsonProperty("inets")]
        public string Inets { get; set; }

        [JsonProperty("couch")]
        public string Couch { get; set; }

        [JsonProperty("mapreduce")]
        public string Mapreduce { get; set; }

        [JsonProperty("couch_index_merger")]
        public string CouchIndexMerger { get; set; }

        [JsonProperty("kernel")]
        public string Kernel { get; set; }

        [JsonProperty("crypto")]
        public string Crypto { get; set; }

        [JsonProperty("ssl")]
        public string Ssl { get; set; }

        [JsonProperty("sasl")]
        public string Sasl { get; set; }

        [JsonProperty("couch_view_parser")]
        public string CouchViewParser { get; set; }

        [JsonProperty("ns_server")]
        public string NsServer { get; set; }

        [JsonProperty("mochiweb")]
        public string Mochiweb { get; set; }

        [JsonProperty("syntax_tools")]
        public string SyntaxTools { get; set; }

        [JsonProperty("xmerl")]
        public string Xmerl { get; set; }

        [JsonProperty("oauth")]
        public string Oauth { get; set; }

        [JsonProperty("stdlib")]
        public string Stdlib { get; set; }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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