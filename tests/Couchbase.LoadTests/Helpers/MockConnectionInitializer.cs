using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Connections;

namespace Couchbase.LoadTests.Helpers
{
    internal class MockConnectionInitializer : IConnectionInitializer
    {
        public IPEndPoint EndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 8091);

        public Task InitializeConnectionAsync(IConnection connection, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SelectBucketAsync(IConnection connection, string name, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
