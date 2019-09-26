using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Couchbase.Utils;

namespace Couchbase.Core.Configuration.Server
{
    internal class NodeAdapter
    {
        private readonly ConcurrentDictionary<string, IPEndPoint> _cachedEndPoints = new ConcurrentDictionary<string, IPEndPoint>();
        private IPAddress _cachedIpAddress;

        public NodeAdapter(Node node, NodesExt nodeExt, BucketConfig bucketConfig)
        {
            var node1 = node;

            // get hostname (uses alternate network hostname if configured)
            Hostname = GetHostname(node, nodeExt, bucketConfig);

            if (node != null)
            {
                CouchbaseApiBase = node.CouchApiBase.Replace(ConstantsString.HostPlaceHolder, Hostname);
                CouchbaseApiBaseHttps = node.CouchApiBaseHttps;
            }

            if (nodeExt != null)
            {
                // get services, will use alternate network ports if configured
                var services = GetServicePorts(nodeExt, bucketConfig);

                // if not using alternate network and node is null
                if (!UseAlternateNetwork(nodeExt, bucketConfig) && node == null)
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

        private string GetHostname(Node node, NodesExt nodeExt, BucketConfig bucketConfig)
        {
            // try to use NodeExt hostname (uses alternate network if configured)
            var hostname = GetHostname(nodeExt, bucketConfig);

            // if hostname is invalid, use node's hostname
            if (string.IsNullOrWhiteSpace(hostname))
            {
                hostname = node.Hostname;
            }

            if (hostname.Contains(ConstantsString.HostPlaceHolder))
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
            if (string.IsNullOrWhiteSpace(nodeExt?.AlternateAddresses?.External?.Hostname))
            {
                return false;
            }

            switch (bucketConfig.NetworkType)
            {
                case NetworkTypes.Auto:
                    // use alternate network if bucket config and nodeExt's hostname don't match
                    return string.Compare(nodeExt.Hostname, bucketConfig.SurrogateHost, StringComparison.Ordinal) != 0;
                case NetworkTypes.External:
                case NetworkTypes.Default:
                    return true;
                default:
                    return false;
            }
        }

        internal string GetHostname(NodesExt nodeExt, BucketConfig bucketConfig)
        {
            if (UseAlternateNetwork(nodeExt, bucketConfig))
            {
                return nodeExt.AlternateAddresses.External.Hostname;
            }

            return nodeExt?.Hostname;
        }

        internal Services GetServicePorts(NodesExt nodeExt, BucketConfig bucketConfig)
        {
            if (UseAlternateNetwork(nodeExt, bucketConfig) &&
                nodeExt.AlternateAddresses?.External?.Ports != null)
            {
                return nodeExt.AlternateAddresses.External.Ports;
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

        /// <summary>
        /// Gets the <see cref="IPEndPoint" /> for the KV port for this node.
        /// </summary>
        /// <returns>
        /// An <see cref="IPEndPoint" /> with the KV port.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public IPEndPoint GetIpEndPoint()
        {
            return GetIpEndPoint(KeyValue);
        }

        /// <summary>
        /// Gets the <see cref="T:System.Net.IPEndPoint" /> for the KV port for this node.
        /// </summary>
        /// <param name="port">The port for the <see cref="T:System.Net.IPEndPoint" /></param>
        /// <returns>
        /// An <see cref="T:System.Net.IPEndPoint" /> with the port passed in.
        /// </returns>
        public IPEndPoint GetIpEndPoint(int port)
        {
            var key = Hostname + ":" + port;
            if (!_cachedEndPoints.TryGetValue(key, out var endPoint))
            {
                endPoint = IpEndPointExtensions.GetEndPoint(Hostname, port);
                IsIPv6 = endPoint.AddressFamily == AddressFamily.InterNetworkV6;
                _cachedEndPoints.TryAdd(key, endPoint);
            }
            return endPoint;
        }

        /// <summary>
        /// Gets the <see cref="IPAddress" /> for this node.
        /// </summary>
        /// <returns>
        /// An <see cref="IPAddress" /> for this node.
        /// </returns>
        public IPAddress GetIpAddress()
        {
            return _cachedIpAddress ?? (_cachedIpAddress = GetIpEndPoint().Address);
        }

        /// <summary>
        /// Gets the ip end point.
        /// </summary>
        /// <param name="useSsl">if set to <c>true</c> use SSL/TLS.</param>
        /// <returns></returns>
        public IPEndPoint GetIpEndPoint(bool useSsl)
        {
            return GetIpEndPoint(useSsl ? KeyValueSsl : KeyValue);
        }

        /// <summary>
        /// Gets a value indicating whether this instance is data node.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance is data node; otherwise, <c>false</c>.
        /// </value>
        public bool IsDataNode => KeyValue > 0 || KeyValueSsl > 0;

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
        public bool IsQueryNode => N1Ql > 0;

        /// <summary>
        /// Gets a value indicating whether this instance is search node.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is search node; otherwise, <c>false</c>.
        /// </value>
        public bool IsSearchNode => Fts > 0;

        /// <summary>
        /// Gets a value indicating whether this instance is an analytics node.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is analytics node; otherwise, <c>false</c>.
        /// </value>
        public bool IsAnalyticsNode => Analytics > 0 || AnalyticsSsl > 0;

        /// <summary>
        /// True if the endpoint is using IPv6.
        /// </summary>
        public bool IsIPv6 { get; set;  }
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
