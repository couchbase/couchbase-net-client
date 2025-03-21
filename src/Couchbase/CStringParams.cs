using Couchbase.Core.Compatibility;

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
        public const string Compression = "compression";
        public const string CompressionMinSize = "compression_min_size";
        public const string CompressionMinRatio = "compression_min_ratio";
        public const string NetworkResolution = "network";
        public const string PreferredServerGroup = "preferred_server_group";

        [InterfaceStability(Level.Uncommitted)]
        public const string RandomSeedNodes = "random_seed_nodes";
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
