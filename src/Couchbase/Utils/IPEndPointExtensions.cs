using System;
using System.Net;

namespace Couchbase.Utils
{
    internal static class IpEndPointExtensions
    {
        public static string DefaultPort = "8091";

        public static IPEndPoint GetEndPoint(string hostname, int port, bool useInterNetworkV6Addresses)
        {
            if (!IPAddress.TryParse(hostname, out var ipAddress))
            {
                var uri = new Uri($"http://{hostname}");
                ipAddress = uri.GetIpAddress(useInterNetworkV6Addresses);
                if (ipAddress == null)
                {
                    throw new ArgumentException("ipAddress");
                }
            }
            return new IPEndPoint(ipAddress, port);
        }

        public static IPEndPoint GetEndPoint(string hostname, int port) =>
            GetEndPoint(hostname, port, ClusterOptions.UseInterNetworkV6Addresses);

        private static IPEndPoint GetEndPointFromBasicString(string server, bool preferInterNetworkV6Addresses)
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
            return GetEndPoint(address[0], port, preferInterNetworkV6Addresses);
        }

        private static IPEndPoint GetEndpointFromIpv6String(string server)
        {
            // Assumes an address with IPv6 syntax of "[ip]:port"
            // Since ip will contain colons, we can't just split the string

            const string invalidServer = "{Not a valid IPv6 host/port string";

            var addressEnd = server.IndexOf(']', 1);
            if (addressEnd < 0)
            {
                throw new ArgumentException(invalidServer, nameof(server));
            }

            var address = server.Substring(1, addressEnd - 1);

            if (server.Length < addressEnd + 3 || server[addressEnd + 1] != ':')
            {
                // Doesn't have the port on the end
                throw new ArgumentException(invalidServer, nameof(server));
            }

            var portString = server.Substring(addressEnd + 2);
            if (!int.TryParse(portString, out var port))
            {
                throw new ArgumentException(invalidServer, nameof(server));
            }

            return new IPEndPoint(IPAddress.Parse(address), port);
        }

        /// <summary>
        /// Gets an <see cref="IPEndPoint"/> from a domain:port pair, such as "localhost:11210" or "[::1]:11210".
        /// </summary>
        /// <param name="server">Server to parse.</param>
        /// <param name="preferInterNetworkV6Addresses">For domain names, prefer an IPv6 address.</param>
        /// <returns>The <see cref="IPEndPoint"/>.</returns>
        public static IPEndPoint GetEndPoint(string server, bool preferInterNetworkV6Addresses)
        {
            if (server == null)
            {
                throw new ArgumentNullException(nameof(server));
            }

            return server.StartsWith("[", StringComparison.Ordinal) ?
                GetEndpointFromIpv6String(server) :
                GetEndPointFromBasicString(server, preferInterNetworkV6Addresses);
        }

        /// <summary>
        /// Gets an <see cref="IPEndPoint"/> from a domain:port pair, such as "localhost:11210" or "[::1]:11210".
        /// </summary>
        /// <param name="server">Server to parse.</param>
        /// <returns>The <see cref="IPEndPoint"/>.</returns>
        public static IPEndPoint GetEndPoint(string server) =>
            GetEndPoint(server, ClusterOptions.UseInterNetworkV6Addresses);
    }
}
