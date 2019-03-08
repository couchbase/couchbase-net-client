using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Couchbase.Core.IO.Operations.Legacy.Errors;
using Couchbase.Core.IO.Operations;

namespace Couchbase.Core.IO
{
    public sealed class SocketAsyncState : IDisposable
    {
        /// <summary>
        /// Represents a response status that has originated in within the client.
        /// The purpose is to handle client side errors
        /// </summary>
        public ResponseStatus Status { get; set; }

        public IPEndPoint EndPoint { get; set; }

        public MemoryStream Data { get; set; }

        public uint Opaque { get; set; }

        public string ConnectionId { get; set; }

        public string LocalEndpoint { get; set; }

        public ErrorMap ErrorMap { get; set; }

        public Exception Exception { get; set; }

        public Func<SocketAsyncState, Task> Completed { get; set; }

        public void Dispose()
        {
            Data?.Dispose();
        }
    }
}
