using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Couchbase.Utils
{
    /// <summary>
    /// Provides extension methods for working with <see cref="Uri"/> class instances.
    /// </summary>
    public static class UriExtensions
    {
        public static IPAddress GetIpAddress(this Uri uri)
        {
            IPAddress ipAddress = null;
            var hostEntry = Dns.GetHostEntry(uri.Host);
            foreach (var host in hostEntry.AddressList)
            {
                if (host.AddressFamily != AddressFamily.InterNetwork) continue;
                ipAddress = host;
                break;
            }
            if (ipAddress == null)
            {
                throw new UnsupportedAddressFamilyException(uri.OriginalString);
            }
            return ipAddress;
        }

        /// <summary>
        /// Gets an <see cref="IPEndPoint"/> given a valid <see cref="Uri"/> instance and port.
        /// </summary>
        /// <param name="uri">The <see cref="Uri"/> to use to get the <see cref="IPEndPoint"/></param>
        /// <param name="port">The network port to use.</param>
        /// <returns>An <see cref="IPEndPoint"/> reference.</returns>
// ReSharper disable once InconsistentNaming
        public static IPEndPoint GetIPEndPoint(this Uri uri, int port)
        {
            var ipAddress = uri.GetIpAddress();
            return new IPEndPoint(ipAddress, port);
        }

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
                throw new ArgumentException("ipAddress");
            }
            int port;
            if (!int.TryParse(address[1], out port))
            {
                throw new ArgumentException("port");
            }
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