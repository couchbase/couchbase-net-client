using System;
using System.Buffers;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Errors;
using Couchbase.Utils;

namespace Couchbase.Core.IO
{
     /// <summary>
    /// Represents an asynchronous Memcached request in flight.
    /// </summary>
    internal class AsyncState : IState
    {
        private readonly TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>();

        public IPEndPoint EndPoint { get; set; }
        public Action<SocketAsyncState> Callback { get; set; }
        public uint Opaque { get; set; }
        public Timer Timer;
        public ulong ConnectionId { get; set; }
        public ErrorMap ErrorMap { get; set; }
        public string LocalEndpoint { get; set; }

        public Task CompletionTask => _tcs.Task;

        /// <summary>
        /// Cancels the current Memcached request that is in-flight.
        /// </summary>
        public void Cancel(ResponseStatus status, Exception e = null)
        {
            Timer?.Dispose();

            var response = MemoryPool<byte>.Shared.RentAndSlice(24);
            ByteConverter.FromUInt32(Opaque, response.Memory.Span.Slice(HeaderOffsets.Opaque));

            var state = new SocketAsyncState
            {
                Opaque = Opaque,
                // ReSharper disable once MergeConditionalExpression
                Exception = e,
                Status = status,
                EndPoint = EndPoint,
                ConnectionId = ConnectionId,
                LocalEndpoint = LocalEndpoint
            };

            state.SetData(response);

            Callback(state);

            _tcs.TrySetCanceled();
        }

        /// <inheritdoc />
        public void Complete(IMemoryOwner<byte> response)
        {
            Timer?.Dispose();

            ResponseStatus status;
            Exception e = null;

            if (response == null)
            {
                //this means the request never completed - assume a transport failure
                response = MemoryPool<byte>.Shared.RentAndSlice(24);
                ByteConverter.FromUInt32(Opaque, response.Memory.Span.Slice(HeaderOffsets.Opaque));
                e = new NetworkErrorException("The socket connection was closed.");
                status = ResponseStatus.TransportFailure;
            }
            else
            {
                //defaults
                status = (ResponseStatus) ByteConverter.ToInt16(response.Memory.Span.Slice(HeaderOffsets.Status));
            }

            var state = new SocketAsyncState
            {
                Opaque = Opaque,
                Exception = e,
                Status = status,
                EndPoint = EndPoint,
                ConnectionId = ConnectionId,
                LocalEndpoint = LocalEndpoint
            };

            state.SetData(response);

            //somewhat of hack for backwards compatibility
            Task.Factory.StartNew(stateObj => Callback((SocketAsyncState) stateObj), state);

            _tcs.TrySetResult(true);
        }

        public void Dispose()
        {
            Timer?.Dispose();
        }
    }
}
