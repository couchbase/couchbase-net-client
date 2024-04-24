using System;
using System.Linq;

namespace Couchbase.Core.Configuration.Server
{
    internal class NodeAdapter
    {
        private string _mappedNodeInfo = string.Empty;

        public NodeAdapter()
        {
        }

        public NodeAdapter(Node node, NodesExt nodeExt, BucketConfig bucketConfig)
        {
            ConfigVersion = bucketConfig.ConfigVersion;

            var node1 = node;

            //Detect if we should be using alternate addresses for hostname
            UseAlternateAddress = UseAlternateNetwork(nodeExt, bucketConfig);

            // get hostname (uses alternate network hostname if configured)
            Hostname = GetHostname(node, nodeExt, bucketConfig);

            if (node != null)
            {
                CouchbaseApiBase = node.CouchApiBase.Replace("$HOST", Hostname);
                CouchbaseApiBaseHttps = node.CouchApiBaseHttps;
            }

            if (nodeExt != null)
            {
                // get services, will use alternate network ports if configured
                var services = GetServicePorts(nodeExt, bucketConfig);

                // if not using alternate network and node is null
                if (!UseAlternateNetwork(nodeExt, bucketConfig) && node == null && !bucketConfig.IsGlobal)
                {
                    // if using nodeExt and node is null, the KV service may not be available yet and should be disabled (set to 0)
                    // this prevents the server's data service being marked as active before it is ready
                    // see https://issues.couchbase.com/browse/JVMCBC-564, https://issues.couchbase.com/browse/NCBC-1791 and
                    // https://issues.couchbase.com/browse/NCBC-1808 for more details
                    services.Kv = 0;
                    services.KvSsl = 0;
                }

                ConfigureServicePorts(services);
            }
            else
            {
                if (node1 != null)
                {
                    MgmtApiSsl = node1.Ports.HttpsMgmt;
                    Moxi = node1.Ports.Proxy;
                    KeyValue = node1.Ports.Direct;
                    KeyValueSsl = node1.Ports.SslDirect;
                    ViewsSsl = node1.Ports.HttpsCapi;
                }

                Views = new Uri(CouchbaseApiBase).Port;
            }
        }

        public ConfigVersion ConfigVersion { get; internal set; }

        private bool UseAlternateAddress { get; set; }

        private string GetHostname(Node node, NodesExt nodeExt, BucketConfig bucketConfig)
        {
            // try to use NodeExt hostname (uses alternate network if configured)
            var hostname = GetHostname(nodeExt, bucketConfig);

            // if hostname is invalid, use node's hostname
            if (string.IsNullOrWhiteSpace(hostname))
            {
                hostname = node.Hostname;
            }

            if (hostname.Contains("$HOST"))
            {
                return "localhost";
            }

            var parts = hostname.Split(':');
            switch (parts.Length)
            {
                case 1: // hostname or IPv4 no port
                    hostname = parts[0];
                    break;
                case 2: // hostname or IPv4 with port
                    hostname = parts[0];
                    break;
                default: // IPv6
                    // is it [a:b:c:d]:<port>
                    if (parts.First().StartsWith("[") && parts[parts.Length - 2].EndsWith("]"))
                    {
                        hostname = string.Join(":", parts.Take(parts.Length - 1));
                    }
                    else
                    {
                        hostname = string.Join(":", parts);
                    }

                    break;
            }

            return hostname;
        }

        internal bool UseAlternateNetwork(NodesExt nodeExt, BucketConfig bucketConfig)
        {
            // make sure we have at least an alternate network hostname (alternate ports are optional)
            if (nodeExt == null || !nodeExt.HasAlternateAddress || bucketConfig.NetworkResolution == NetworkResolution.Default)
            {
                return false;
            }

            if (bucketConfig.NetworkResolution == NetworkResolution.Auto || bucketConfig.NetworkResolution == NetworkResolution.External)
            {
                return string.Compare(nodeExt.Hostname,
                    nodeExt.AlternateAddresses[NetworkResolution.External].Hostname,
                    StringComparison.InvariantCultureIgnoreCase) != 0 || nodeExt.HasAlternateAddress;
            }

            //It's a custom configuration being used - try it.
            if (nodeExt.AlternateAddresses.ContainsKey(bucketConfig.NetworkResolution))
            {
                return true;
            }

            throw new CouchbaseException($"Cannot resolve NetworkResolution - {bucketConfig.NetworkResolution}");
        }

        internal string GetHostname(NodesExt nodeExt, BucketConfig bucketConfig)
        {
            if (UseAlternateAddress)
            {
                var networkResolution = bucketConfig.NetworkResolution == NetworkResolution.Auto
                    ? NetworkResolution.External : bucketConfig.NetworkResolution;

                var hostname = nodeExt.AlternateAddresses[networkResolution].Hostname;
                _mappedNodeInfo = $"NetworkResolution [{bucketConfig.NetworkResolution}] using alternate mapping {nodeExt.Hostname} to {hostname} - rev {bucketConfig.ConfigVersion}.";
                return hostname;
            }

            _mappedNodeInfo = $"NetworkResolution [{bucketConfig.NetworkResolution}] using default {nodeExt?.Hostname} - rev {bucketConfig.ConfigVersion}";
            return nodeExt?.Hostname;
        }

        internal Services GetServicePorts(NodesExt nodeExt, BucketConfig bucketConfig)
        {
            if (UseAlternateAddress)
            {
                var networkResolution = bucketConfig.NetworkResolution == NetworkResolution.Auto
                    ? NetworkResolution.External : bucketConfig.NetworkResolution;

                var ports = nodeExt.AlternateAddresses[networkResolution].Ports;
                if (ports != null)
                {
                    return ports;
                }
            }

            return nodeExt?.Services;
        }

        private void ConfigureServicePorts(Services services)
        {
            MgmtApi = services.Mgmt;
            MgmtApiSsl = services.MgmtSsl;
            KeyValue = services.Kv;
            KeyValueSsl = services.KvSsl;
            Views = services.Capi;
            ViewsSsl = services.CapiSsl;
            Moxi = services.Moxi;
            Projector = services.Projector;
            IndexAdmin = services.IndexAdmin;
            IndexScan = services.IndexScan;
            IndexHttp = services.IndexHttp;
            IndexStreamInit = services.IndexStreamInit;
            IndexStreamCatchup = services.IndexStreamCatchup;
            IndexStreamMaint = services.IndexStreamMaint;
            N1Ql = services.N1Ql;
            N1QlSsl = services.N1QlSsl;
            Fts = services.Fts;
            FtsSsl = services.FtsSsl;
            Analytics = services.Cbas;
            AnalyticsSsl = services.CbasSsl;
            Eventing = services.EventingAdminPort;
            EventingSsl = services.EventingSSL;
            IsKvNode = KeyValue > 0 || KeyValueSsl > 0;
        }

        public string Hostname { get; set; }
        public string CouchbaseApiBase { get; set; }
        public string CouchbaseApiBaseHttps { get; set; }
        public int MgmtApi { get; set; }
        public int MgmtApiSsl { get; set; }
        public int Views { get; set; }
        public int ViewsSsl { get; set; }
        public int Moxi { get; set; }
        public int KeyValue { get; set; }
        public int KeyValueSsl { get; set; }
        public int Projector { get; set; }
        public int IndexAdmin { get; set; }
        public int IndexScan { get; set; }
        public int IndexHttp { get; set; }
        public int IndexStreamInit { get; set; }
        public int IndexStreamCatchup { get; set; }
        public int IndexStreamMaint { get; set; }
        public int N1Ql { get; set; }
        public int N1QlSsl { get; set; }
        public int Fts { get; set; }
        public int FtsSsl { get; set; }
        public int Analytics { get; set; }
        public int AnalyticsSsl { get; set; }
        public int Eventing { get; set; }
        public int EventingSsl { get; set; }

        /// <summary>
        /// Gets a value indicating whether this instance is view node.
        /// </summary>
        /// <value>
        ///     <c>true</c> if this instance is view node; otherwise, <c>false</c>.
        /// </value>
        public bool IsViewNode => Views > 0 || ViewsSsl > 0;

        /// <summary>
        /// Gets a value indicating whether this instance is data node.
        /// </summary>
        /// <value>
        ///     <c>true</c> if this instance is data node; otherwise, <c>false</c>.
        /// </value>
        public bool IsKvNode { get; private set; } = true;

        /// <summary>
        /// Gets a value indicating whether this instance is index node.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance is index node; otherwise, <c>false</c>.
        /// </value>
        public bool IsIndexNode => IndexHttp > 0;

        /// <summary>
        /// Gets a value indicating whether this instance is query node.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance is query node; otherwise, <c>false</c>.
        /// </value>
        public bool IsQueryNode => N1Ql > 0 || N1QlSsl > 0;

        /// <summary>
        /// Gets a value indicating whether this instance is search node.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is search node; otherwise, <c>false</c>.
        /// </value>
        public bool IsSearchNode => Fts > 0 || FtsSsl > 0;

        /// <summary>
        /// Gets a value indicating whether this instance is an analytics node.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is analytics node; otherwise, <c>false</c>.
        /// </value>
        public bool IsAnalyticsNode => Analytics > 0 || AnalyticsSsl > 0;

        /// <summary>
        /// Gets a value indicating if this instance is an Eventing Service node.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is eventing node; otherwise, <c>false</c>.
        /// </value>
        public bool IsEventingNode => Eventing > 0 || EventingSsl > 0;

        /// <summary>
        /// Gets a value indicating if this instance is a Management service node.
        /// </summary>
        /// /// <value>
        /// <c>true</c> if this instance is management node; otherwise, <c>false</c>.
        /// </value>
        public bool IsManagementNode => MgmtApi > 0 || MgmtApiSsl > 0;

        public override string ToString()
        {
            return _mappedNodeInfo;
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2019 Couchbase, Inc.
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
