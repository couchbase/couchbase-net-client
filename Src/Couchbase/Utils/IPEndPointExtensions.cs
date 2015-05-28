using System;
using System.Linq;
using System.Net;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;

namespace Couchbase.Utils
{
// ReSharper disable once InconsistentNaming
    internal static class IPEndPointExtensions
    {
        public static IPEndPoint GetEndPoint(string server)
        {
            const int maxSplits = 2;
            var address = server.Split(':');
            if (address.Count() != maxSplits)
            {
                throw new ArgumentException("server");
            }
            IPAddress ipAddress;
            if (!IPAddress.TryParse(address[0], out ipAddress))
            {
                var uri = new Uri(String.Format("http://{0}", address[0]));
                ipAddress = uri.GetIpAddress();
                if (ipAddress == null)
                {
                    throw new ArgumentException("ipAddress");
                }
            }
            int port;
            if (!int.TryParse(address[1], out port))
            {
                throw new ArgumentException("port");
            }
            return new IPEndPoint(ipAddress, port);
        }

        public static IPEndPoint GetEndPoint(INodeAdapter adapter, BucketConfiguration clientConfig, IBucketConfig server)
        {
            var address = adapter.Hostname.Split(':').First();
            IPAddress ipAddress;
            if (!IPAddress.TryParse(address, out ipAddress))
            {
                var uri = new Uri(String.Format("http://{0}", address));
                ipAddress = uri.GetIpAddress();
                if (ipAddress == null)
                {
                    throw new ArgumentException("ipAddress");
                }
            }
            var port = clientConfig.UseSsl ? adapter.KeyValueSsl : adapter.KeyValue;
            return new IPEndPoint(ipAddress, port);
        }


        public static IPEndPoint GetEndPoint(Node node, BucketConfiguration clientConfig, IBucketConfig serverConfig)
        {
            var address = node.Hostname.Split(':').First();
            IPAddress ipAddress;
            if (!IPAddress.TryParse(address, out ipAddress))
            {
                var uri = new Uri(String.Format("http://{0}", address));
                ipAddress = uri.GetIpAddress();
                if (ipAddress == null)
                {
                    throw new ArgumentException("ipAddress");
                }
            }
            var port = clientConfig.UseSsl ? node.Ports.SslDirect : node.Ports.Direct;
            return new IPEndPoint(ipAddress, port);
        }

        public static IPEndPoint GetEndPoint(NodeExt nodeExt, BucketConfiguration bucketConfig, IBucketConfig serverConfig)
        {
            var address = nodeExt.Hostname.Split(':').First();
            IPAddress ipAddress;
            if (!IPAddress.TryParse(address, out ipAddress))
            {
                var uri = new Uri(String.Format("http://{0}", address));
                ipAddress = uri.GetIpAddress();
                if (ipAddress == null)
                {
                    throw new ArgumentException("ipAddress");
                }
            }
            var port = bucketConfig.UseSsl ? nodeExt.Services.KvSSL : nodeExt.Services.KV;
            return new IPEndPoint(ipAddress, port);
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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