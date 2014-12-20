using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration;
using Couchbase.Configuration.Server.Serialization;

namespace Couchbase.Core
{
    internal class NodeAdapter : INodeAdapter
    {
        private Node _node;
        private NodeExt _nodeExt;

        public NodeAdapter(Node node, NodeExt nodeExt)
        {
            _node = node;
            _nodeExt = nodeExt;

            //normalize the interfaces providing defaults where applicable
            Hostname = nodeExt == null ? node.Hostname : nodeExt.Hostname;
            Hostname = Hostname ?? node.Hostname;
            CouchbaseApiBase = node.CouchApiBase;
            MgmtApi = nodeExt == null ? (int)DefaultPorts.MgmtApi : nodeExt.Services.Mgmt;
            MgmtApiSsl = nodeExt == null ? node.Ports.HttpsMgmt : nodeExt.Services.MgmtSSL;
            Views = nodeExt == null ? (int) DefaultPorts.CApi : nodeExt.Services.Capi;
            ViewsSsl = nodeExt == null ? node.Ports.HttpsCapi : nodeExt.Services.CapiSSL;
            Moxi = nodeExt == null ? node.Ports.Proxy : nodeExt.Services.Moxi;
            KeyValue = nodeExt == null ? node.Ports.Direct : nodeExt.Services.KV;
            KeyValueSsl = nodeExt == null ? node.Ports.SslDirect : nodeExt.Services.KvSSL;

            CouchbaseApiBase = _node.CouchApiBase;
            CouchbaseApiBaseHttps = _node.CouchApiBaseHttps;
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
    }
}
