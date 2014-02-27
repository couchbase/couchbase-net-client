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