namespace Couchbase.Tracing
{
    internal static class CouchbaseTags
    {
        public const string DbTypeCouchbase = "couchbase";

        public const string Service = "couchbase.service";
        public const string ServiceKv = "kv";
        public const string ServiceView = "view";
        public const string ServiceN1ql = "n1ql";
        public const string ServiceSearch = "search";
        public const string ServiceAnalytics = "analytics";

        public const string OperationId = "couchbase.operation_id";
        public const string Ignore = "couchbase.ignore";

        public const string LocalAddress = "local.address";
        public const string PeerLatency = "peer.latency";
    }
}
