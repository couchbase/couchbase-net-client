using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Connections;

namespace Couchbase.LoadTests.Helpers
{
    internal class MockConnectionFactory : IConnectionFactory
    {
        public Task<IConnection> CreateAndConnectAsync(HostEndpointWithPort hostEndpoint, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IConnection>(new MockConnection());
        }
    }
}
