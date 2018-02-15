using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Couchbase.Logging;
using Couchbase.Configuration.Client;

namespace Couchbase.Utils
{
    /// <summary>
    /// Provides extension methods for working with <see cref="Uri"/> class instances.
    /// </summary>
    public static class UriExtensions
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(UriExtensions));

        public static Uri ReplaceCouchbaseSchemeWithHttp(this Uri uri, ClientConfiguration config, string bucketName)
        {
            if (uri.Scheme == "couchbase")
            {
                var useSsl = config.BucketConfigs.TryGetValue(bucketName, out var bucketConfig) ? bucketConfig.UseSsl : config.UseSsl;
                var newUri = new UriBuilder(uri) { Scheme = useSsl ? "https" : "http" };
                return newUri.Uri;
            }
            return uri;
        }

        /// <summary>
        /// Resolves a given <see cref="Uri"/> to an <see cref="IPAddress"/> using DNS if necessary.
        /// </summary>
        /// <param name="uri">The <see cref="Uri"/> to resolve the <see cref="IPAddress"/> from.</param>
        /// <param name="useInterNetworkV6Addresses"></param>
        /// <returns>An <see cref="IPAddress"/> reference.</returns>
        /// <remarks>Only returns IPV4 Addresses unless <see cref="useInterNetworkV6Addresses"/> is true!</remarks>
        public static IPAddress GetIpAddress(this Uri uri, bool useInterNetworkV6Addresses)
        {
            if (!IPAddress.TryParse(uri.Host, out var ipAddress))
            {
                try
                {
#if NETSTANDARD15
                    IPHostEntry hostEntry;
                    using (new SynchronizationContextExclusion())
                    {
                        hostEntry = Dns.GetHostEntryAsync(uri.DnsSafeHost).Result;
                    }
#else
                    var hostEntry = Dns.GetHostEntry(uri.DnsSafeHost);
#endif

                    //use ip6 addresses only if configured
                    var hosts = useInterNetworkV6Addresses
                        ? hostEntry.AddressList.Where(x => x.AddressFamily == AddressFamily.InterNetworkV6)
                        : hostEntry.AddressList.Where(x => x.AddressFamily == AddressFamily.InterNetwork);

                    foreach (var host in hosts)
                    {
                        ipAddress = host;
                        break;
                    }

                    //default back to IPv4 addresses if no IPv6 can be resolved
                    if (useInterNetworkV6Addresses && ipAddress == null)
                    {
                        hosts = hostEntry.AddressList.Where(x => x.AddressFamily == AddressFamily.InterNetwork);
                        foreach (var host in hosts)
                        {
                            ipAddress = host;
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error("Could not resolve hostname to IP", e);
                }
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
            var ipAddress = uri.GetIpAddress(ClientConfiguration.UseInterNetworkV6Addresses);
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
                var uri = new Uri(String.Format("http://{0}", server));
                ipAddress = uri.GetIpAddress(ClientConfiguration.UseInterNetworkV6Addresses);
                if(ipAddress == null)
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