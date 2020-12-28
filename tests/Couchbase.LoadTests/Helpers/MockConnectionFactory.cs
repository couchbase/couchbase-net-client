using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Connections;

namespace Couchbase.LoadTests.Helpers
{
    internal class MockConnectionFactory : IConnectionFactory
    {
        public Task<IConnection> CreateAndConnectAsync(IPEndPoint endPoint, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IConnection>(new MockConnection());
        }
    }
}
