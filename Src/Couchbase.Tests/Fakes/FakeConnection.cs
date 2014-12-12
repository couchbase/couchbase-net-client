using System;
using System.Net.Sockets;
using Couchbase.IO;
using Couchbase.IO.Operations;

namespace Couchbase.Tests.Fakes
{
    internal class FakeConnection : IConnection
    {
        private readonly Guid _identity = Guid.NewGuid();
        private readonly Socket _socket;

        public Socket Socket
        {
            get { return _socket; }
        }

        public Guid Identity
        {
            get { return _identity; }
        }

        public bool IsAuthenticated { get; set; }

        public void Send<T>(IOperation<T> operation)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public bool IsSecure { get; protected set; }


        public System.Net.EndPoint EndPoint
        {
            get { throw new NotImplementedException(); }
        }


        public bool IsDead
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }


        public System.Threading.Tasks.Task<uint> SendAsync(byte[] buffer)
        {
            throw new NotImplementedException();
        }

        public System.Threading.Tasks.Task<byte[]> ReceiveAsync(uint opaque)
        {
            throw new NotImplementedException();
        }


        public Couchbase.IO.OperationAsyncState State
        {
            get { throw new NotImplementedException(); }
        }


        public byte[] Send(byte[] request)
        {
            throw new NotImplementedException();
        }
    }
}
