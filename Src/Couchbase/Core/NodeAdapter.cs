using System.Collections.Concurrent;
using System.Net;
using Couchbase.Configuration;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Utils;
using Newtonsoft.Json;

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
                Hostname = Hostname.Split(':')[0];
            }

            //These will default to zero id nodesExt is null
            if (nodeExt != null)
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
            }

            if (_node != null)
            {
                CouchbaseApiBase = _node.CouchApiBase;
                CouchbaseApiBaseHttps = _node.CouchApiBaseHttps;
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
    }
}
