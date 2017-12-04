using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Utils;

namespace Couchbase.Core
{
    internal class NodeAdapter : INodeAdapter
    {
        private Node _node;
        private NodeExt _nodeExt;
        private readonly ConcurrentDictionary<string, IPEndPoint> _cachedEndPoints = new ConcurrentDictionary<string, IPEndPoint>();
        private IPAddress _cachedIPAddress;

        public NodeAdapter(Node node, NodeExt nodeExt)
        {
            _node = node;
            _nodeExt = nodeExt;

            Hostname = GetHostname(node, nodeExt);

            if (_node != null)
            {
                CouchbaseApiBase = _node.CouchApiBase.Replace("$HOST", Hostname);
                CouchbaseApiBaseHttps = _node.CouchApiBaseHttps;
            }
            if (nodeExt == null)
            {
                MgmtApiSsl = node.Ports.HttpsMgmt;
                Moxi = node.Ports.Proxy;
                KeyValue = node.Ports.Direct;
                KeyValueSsl = node.Ports.SslDirect;
                ViewsSsl = node.Ports.HttpsCapi;
                Views = new Uri(CouchbaseApiBase).Port;
            }
            else
            {
                MgmtApi = _nodeExt.Services.Mgmt;
                MgmtApiSsl = _nodeExt.Services.MgmtSSL;
                Views = _nodeExt.Services.Capi;
                ViewsSsl = _nodeExt.Services.CapiSSL;
                Moxi = _nodeExt.Services.Moxi;
                KeyValue = _nodeExt.Services.KV;
                KeyValueSsl = _nodeExt.Services.KvSSL;
                Projector = _nodeExt.Services.Projector;
                IndexAdmin = _nodeExt.Services.IndexAdmin;
                IndexScan = _nodeExt.Services.IndexScan;
                IndexHttp = _nodeExt.Services.IndexHttp;
                IndexStreamInit = _nodeExt.Services.IndexStreamInit;
                IndexStreamCatchup = _nodeExt.Services.IndexStreamCatchup;
                IndexStreamMaint = _nodeExt.Services.IndexStreamMaint;
                N1QL = _nodeExt.Services.N1QL;
                N1QLSsl = _nodeExt.Services.N1QLSsl;
                Fts = _nodeExt.Services.Fts;
                Analytics = _nodeExt.Services.Analytics;
                AnalyticsSsl = _nodeExt.Services.AnalyticsSsl;
            }
        }

        private static string GetHostname(Node node, NodeExt nodeExt)
        {
            var hostname = string.IsNullOrWhiteSpace(nodeExt?.Hostname) ? node.Hostname : nodeExt.Hostname;
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

        public int N1QL { get; set; }

        public int N1QLSsl { get; set; }

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
        public IPEndPoint GetIPEndPoint()
        {
            return GetIPEndPoint(KeyValue);
        }

        /// <summary>
        /// Gets the <see cref="T:System.Net.IPEndPoint" /> for the KV port for this node.
        /// </summary>
        /// <param name="port">The port for the <see cref="T:System.Net.IPEndPoint" /></param>
        /// <returns>
        /// An <see cref="T:System.Net.IPEndPoint" /> with the port passed in.
        /// </returns>
        public IPEndPoint GetIPEndPoint(int port)
        {
            var key = Hostname + ":" + port;
            if (!_cachedEndPoints.TryGetValue(key, out var endPoint))
            {
                endPoint = IPEndPointExtensions.GetEndPoint(Hostname, port);
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
        public IPAddress GetIPAddress()
        {
            return _cachedIPAddress ?? (_cachedIPAddress = GetIPEndPoint().Address);
        }

        /// <summary>
        /// Gets the ip end point.
        /// </summary>
        /// <param name="useSsl">if set to <c>true</c> use SSL/TLS.</param>
        /// <returns></returns>
        public IPEndPoint GetIPEndPoint(bool useSsl)
        {
            return GetIPEndPoint(useSsl ? KeyValueSsl : KeyValue);
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
        public bool IsQueryNode => N1QL > 0;

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
