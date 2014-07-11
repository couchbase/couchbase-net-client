using System;
using System.Net.Sockets;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.IO.Strategies.Awaitable;

namespace Couchbase.Tests.Fakes
{
    internal class FakeConnection : IConnection
    {
        private readonly Guid _identity = Guid.NewGuid();
        private readonly Socket _socket;
        private readonly OperationAsyncState _state;

        public Socket Socket
        {
            get { return _socket; }
        }

        public Guid Identity
        {
            get { return _identity; }
        }

        public bool IsAuthenticated { get; set; }

        public IOperationResult<T> Send<T>(IOperation<T> operation)
        {
            throw new NotImplementedException();
        }

        public OperationAsyncState State
        {
            get { return _state; }
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
