using System.Net;
using System.Threading.Tasks;

namespace Couchbase.Utils
{
    /// <inheritdoc />
    internal class DotNetDnsClient : IDotNetDnsClient
    {
        public Task<IPAddress[]> GetHostAddressesAsync(string hostName)
        {
            return Dns.GetHostAddressesAsync(hostName);
        }
    }
}
