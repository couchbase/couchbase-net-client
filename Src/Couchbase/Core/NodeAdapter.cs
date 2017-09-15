using System;
using System.Collections.Concurrent;
using System.Net;
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

            //normalize the interfaces providing defaults where applicable
            Hostname = nodeExt == null ? node.Hostname : nodeExt.Hostname;
            Hostname = Hostname ?? node.Hostname;

            //strip off the admin port - we can use services
            if (Hostname.Contains(":"))
            {
                var hostAndPorts = Hostname.Split(':');
                Hostname = hostAndPorts[0];
                if (Hostname.Contains("$HOST"))
                {
                    Hostname = "localhost";
                }
                MgmtApi = int.Parse(hostAndPorts[1]);
            }
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
        /// Gets the <see cref="IPEndPoint" /> for the KV port for this node.
        /// </summary>
        /// <param name="port">The port for the <see cref="IPEndPoint" /></param>
        /// <returns>
        /// An <see cref="IPEndPoint" /> with the port passed in.
        /// </returns>
        public IPEndPoint GetIPEndPoint(int port)
        {
            var key = Hostname + ":" + port;
            IPEndPoint endPoint;
            if (!_cachedEndPoints.TryGetValue(key, out endPoint))
            {
                endPoint = IPEndPointExtensions.GetEndPoint(Hostname + ":" + port);
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
            if (_cachedIPAddress == null)
            {
                _cachedIPAddress = GetIPEndPoint().Address;
            }
            return _cachedIPAddress;
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
        public bool IsDataNode
        {
            get { return KeyValue > 0 || KeyValueSsl > 0; }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is index node.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance is index node; otherwise, <c>false</c>.
        /// </value>
        public bool IsIndexNode
        {
            get { return IndexHttp > 0; }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is query node.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance is query node; otherwise, <c>false</c>.
        /// </value>
        public bool IsQueryNode
        {
            get { return N1QL > 0; }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is search node.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is search node; otherwise, <c>false</c>.
        /// </value>
        public bool IsSearchNode
        {
            get { return Fts > 0; }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is an analytics node.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is analytics node; otherwise, <c>false</c>.
        /// </value>
        public bool IsAnalyticsNode
        {
            get { return Analytics > 0 || AnalyticsSsl > 0; }
        }
    }
}
