using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

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
