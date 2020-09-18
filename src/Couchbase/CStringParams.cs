namespace Couchbase
{
    internal static class CStringParams
    {
        public const string KvConnectTimeout = "kv_connect_timeout";
        public const string KvTimeout = "kv_timeout";
        public const string KvDurableTimeout = "kv_durable_timeout";
        public const string ViewTimeout = "view_timeout";
        public const string QueryTimeout = "query_timeout";
        public const string AnalyticsTimeout = "analytics_timeout";
        public const string SearchTimeout = "search_timeout";
        public const string ManagementTimeout = "management_timeout";
        public const string EnableTls = "enable_tls";
        public const string EnableMutationTokens = "enable_mutation_tokens";
        public const string TcpKeepAliveTime = "tcp_keepalive_time";
        public const string TcpKeepAliveInterval = "tcp_keepalive_interval";
        public const string EnableTcpKeepAlives = "enable_tcp_keepalives";
        public const string ForceIpv4 = "force_ipv4";
        public const string ConfigPollInterval = "config_poll_interval";
        public const string ConfigPollFloorInterval = "config_poll_floor_interval";
        public const string ConfigIdleRedialTimeout = "config_idle_redial_timeout";
        public const string NumKvConnections = "num_kv_connections";
        public const string MaxKvConnections = "max_kv_connections";
        public const string MaxHttpConnections = "max_http_connections";
        public const string IdleHttpConnectionTimeout = "idle_http_connection_timeout";
    }
}
