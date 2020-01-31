using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Connections;

namespace Couchbase.Utils
{
    internal static class IpEndPointExtensions
    {
        public static string DefaultPort = "8091";

        public static IPEndPoint GetEndPoint(string hostname, int port)
        {
            if (!IPAddress.TryParse(hostname, out var ipAddress))
            {
                var uri = new Uri($"http://{hostname}");
                ipAddress = uri.GetIpAddress(ClusterOptions.UseInterNetworkV6Addresses);
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
            string address;
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
            return server.Contains(".") && !server.Contains("[") ?
                GetIPv4EndPoint(server) :
                GetIPv6EndPoint(server);
        }
    }
}
