using System.Net;
using System.Threading.Tasks;

namespace Couchbase.Utils
{
    /// <summary>
    /// Wrapper for <see cref="System.Net.Dns"/> to support mocking.
    /// </summary>
    internal interface IDotNetDnsClient
    {
        Task<IPAddress[]> GetHostAddressesAsync(string hostName);
    }
}
