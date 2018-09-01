using System.Net;

namespace Couchbase.Core
{
    public interface INodeAdapter
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
        // ReSharper disable once InconsistentNaming
        int N1QL { get; set; }
        // ReSharper disable once InconsistentNaming
        int N1QLSsl { get; set; }
        int Fts { get; set; }
        int FtsSsl { get; set; }
        int Analytics { get; set; }
        int AnalyticsSsl { get; set; }

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

        /// <summary>
        /// Gets a value indicating whether this instance is data node which supports K/V and Views.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance is data node ; otherwise, <c>false</c>.
        /// </value>
        bool IsDataNode { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is index node.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance is index node; otherwise, <c>false</c>.
        /// </value>
        bool IsIndexNode { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is query node supports N1QL.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance is query node; otherwise, <c>false</c>.
        /// </value>
        bool IsQueryNode { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is search node.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is search node; otherwise, <c>false</c>.
        /// </value>
        bool IsSearchNode { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is an analytics node.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is analytics node; otherwise, <c>false</c>.
        /// </value>
        bool IsAnalyticsNode { get; }

        /// <summary>
        /// True if the endpoint is using IPv6.
        /// </summary>
        bool IsIPv6 { get; }
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
