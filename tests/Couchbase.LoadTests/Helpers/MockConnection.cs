using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Operations.Errors;

namespace Couchbase.LoadTests.Helpers
{
    internal class MockConnection : IConnection
    {
        public void Dispose()
        {
        }

        public Socket Socket { get; set; }
        public Guid Identity { get; set; }
        public ulong ConnectionId { get; set; }
        public string ContextId { get; set; }
        public bool IsAuthenticated { get; set; }
        public bool IsSecure { get; set; }
        public bool IsConnected { get; set; }
        public EndPoint EndPoint { get; set; }
        public EndPoint LocalEndPoint { get; set; }
        public bool IsDead { get; set; }
        public DateTime? LastActivity { get; set; }

        public Task SendAsync(ReadOnlyMemory<byte> buffer, Func<SocketAsyncState, Task> callback)
        {
            return Task.CompletedTask;
        }

        public Task SendAsync(ReadOnlyMemory<byte> buffer, Func<SocketAsyncState, Task> callback, ErrorMap errorMap)
        {
            return Task.CompletedTask;
        }

        public bool InUse { get; set; }
        public void MarkUsed(bool isUsed)
        {
        }

        public bool IsDisposed { get; set; }
        public bool HasShutdown { get; set; }
        public void Authenticate()
        {
        }

        public bool CheckedForEnhancedAuthentication { get; set; }
        public bool MustEnableServerFeatures { get; set; }
    }
}
