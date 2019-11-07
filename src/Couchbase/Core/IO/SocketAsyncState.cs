using System;
using System.Buffers;
using System.Net;
using System.Threading.Tasks;
using Couchbase.Core.IO.Operations;

namespace Couchbase.Core.IO
{
    public sealed class SocketAsyncState : IDisposable
    {
        private IMemoryOwner<byte> _data;

        /// <summary>
        /// Represents a response status that has originated in within the client.
        /// The purpose is to handle client side errors
        /// </summary>
        public ResponseStatus Status { get; set; }

        public IPEndPoint EndPoint { get; set; }

        public uint Opaque { get; set; }

        public ulong ConnectionId { get; set; }

        public string LocalEndpoint { get; set; }

        public Exception Exception { get; set; }

        public Func<SocketAsyncState, Task> Completed { get; set; }

        public void Dispose()
        {
            _data?.Dispose();
            _data = null;
        }

        /// <summary>
        /// Sets the data for this instance, taking ownership of the data and responsibility for disposing.
        /// </summary>
        /// <param name="data">The <see cref="IMemoryOwner{T}"/>.</param>
        public void SetData(IMemoryOwner<byte> data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        /// <summary>
        /// Extracts the data for this instance, if any. The data is removed, and ownership is transferred to the receiver.
        /// The receiver is now responsible for releasing the memory.
        /// </summary>
        /// <returns>The <see cref="IMemoryOwner{T}"/>.</returns>
        public IMemoryOwner<byte> ExtractData()
        {
            var data = _data;
            _data = null;
            return data;
        }
    }
}
