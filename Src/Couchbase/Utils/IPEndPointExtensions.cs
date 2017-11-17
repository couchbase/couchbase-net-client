using System;
using System.Linq;
using System.Net;
using Couchbase.Configuration.Client;

namespace Couchbase.Utils
{
// ReSharper disable once InconsistentNaming
    internal static class IPEndPointExtensions
    {
        public static string DefaultPort = "8091";

        public static IPEndPoint GetEndPoint(string hostname, int port)
        {
            if (!IPAddress.TryParse(hostname, out var ipAddress))
            {
                var uri = new Uri($"http://{hostname}");
                ipAddress = uri.GetIpAddress(ClientConfiguration.UseInterNetworkV6Addresses);
                if (ipAddress == null)
                {
                    throw new ArgumentException("ipAddress");
                }
            }
            return new IPEndPoint(ipAddress, port);
        }

        public static IPEndPoint GetIPv4EndPoint(string server)
        {
            const int maxSplits = 2;
            var address = server.Split(':');
            if (address.Length != maxSplits)
            {
                throw new ArgumentException("server");
            }
            if (!int.TryParse(address[1], out var port))
            {
                throw new ArgumentException("port");
            }
            return GetEndPoint(address[0], port);
        }

        public static IPEndPoint GetIPv6EndPoint(string server)
        {
            var address = string.Empty;
            var portString = DefaultPort; //we need a port to create the EP

            if (server.Contains("["))
            {
                var startIndex = server.LastIndexOf(':');
                address = server.Substring(0, startIndex);
                portString = server.Substring(startIndex + 1, server.Length - startIndex - 1);
            }
            else
            {
                address = server;
            }

            if (!int.TryParse(portString, out var port))
            {
                throw new ArgumentException("port");
            }

            return GetEndPoint(address, port);
        }

        public static IPEndPoint GetEndPoint(string server)
        {
            return server.Contains('.') && !server.Contains("[") ?
                GetIPv4EndPoint(server) :
                GetIPv6EndPoint(server);
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