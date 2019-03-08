using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Couchbase.Utils
{
    public static class UriExtensions
    {
        public static IPAddress GetIpAddress(this Uri uri, bool useInterNetworkV6Addresses)
        {
            if (!IPAddress.TryParse(uri.Host, out var ipAddress))
            {
                try
                {
                    var hostEntry = Dns.GetHostEntry(uri.DnsSafeHost);

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
                    Console.WriteLine("Could not resolve hostname to IP", e);
                }
            }
            if (ipAddress == null)
            {
                throw new Exception(uri.OriginalString);
            }
            return ipAddress;
        }
    }
}
