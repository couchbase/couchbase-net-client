using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Core
{
    internal interface INodeAdapter
    {
        string Hostname { get; set; }
        string CouchbaseApiBase { get; set; }
        int MgmtApi { get; set; }
        int MgmtApiSsl { get; set; }
        int Views { get; set; }
        int ViewsSsl { get; set; }
        int Moxi { get; set; }
        int KeyValue { get; set; }
        int KeyValueSsl { get; set; }
        int Projector { get; set; }
        int IndexAdmin { get; set; }
        int IndexScan { get; set; }
        int IndexHttp { get; set; }
        int IndexStreamInit { get; set; }
        int IndexStreamCatchup { get; set; }
        int IndexStreamMaint { get; set; }
        int N1QL { get; set; }

        /// <summary>
        /// Gets the <see cref="IPEndPoint"/> for the KV port for this node.
        /// </summary>
        /// <returns>An <see cref="IPEndPoint"/> with the KV port.</returns>
        IPEndPoint GetIPEndPoint();

        /// <summary>
        /// Gets the ip end point.
        /// </summary>
        /// <param name="useSsl">if set to <c>true</c> use SSL/TLS.</param>
        /// <returns></returns>
        IPEndPoint GetIPEndPoint(bool useSsl);

        /// <summary>
        /// Gets the <see cref="IPEndPoint"/> for the KV port for this node.
        /// </summary>
        /// <param name="port">The port for the <see cref="IPEndPoint"/></param>
        /// <returns>An <see cref="IPEndPoint"/> with the port passed in.</returns>
        IPEndPoint GetIPEndPoint(int port);

        /// <summary>
        /// Gets the <see cref="IPAddress"/> for this node.
        /// </summary>
        /// <returns>An <see cref="IPAddress"/> for this node.</returns>
        IPAddress GetIPAddress();
    }
}
