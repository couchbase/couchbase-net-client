using Couchbase.IO;
using Couchbase.IO.Operations;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations.Errors;
using Couchbase.IO.Utils;
using OpenTracing;

namespace Couchbase.Tests.Fakes
{
    internal class FakeConnection : IConnection
    {
        private byte[] _responseBytes;

        public FakeConnection()
        {
            Converter = new DefaultConverter();
        }

        public FakeConnection(Socket socket) : this()
        {
            Socket = socket;
            Identity = Guid.NewGuid();
        }

        public FakeConnection(OperationAsyncState state, EndPoint endPoint, Socket socket) : this()
        {
            Identity = Guid.NewGuid();
            State = state;
            EndPoint = endPoint;
            Socket = socket;
        }

        protected IByteConverter Converter { get; set; }

        public Socket Socket { get; private set; }

        public Guid Identity { get; private set; }

        public ulong ConnectionId { get; private set; }

        public bool IsSecure { get; protected set; }

        public EndPoint EndPoint { get; private set; }
        public EndPoint LocalEndPoint { get; private set; }

        public bool IsDead { get; set; }

        public OperationAsyncState State { get; private set; }

        public bool IsAuthenticated { get; set; }

        public void SetResponse(byte[] reponseBytes)
        {
            _responseBytes = reponseBytes;
        }

        public void Send<T>(IOperation<T> operation)
        {
            throw new NotImplementedException();
        }

        public byte[] Send(byte[] request)
        {
            throw new NotImplementedException();
        }

        public Task SendAsync(byte[] buffer, Func<SocketAsyncState, Task> callback)
        {
            return SendAsync(buffer, callback, null, null);
        }

        public Task SendAsync(byte[] buffer, Func<SocketAsyncState, Task> callback, ISpan dispatchSpan, ErrorMap errorMap)
        {
            var state = new SocketAsyncState
            {
                Data = new MemoryStream(_responseBytes),
                Opaque = Converter.ToUInt32(buffer, HeaderIndexFor.Opaque)
            };
            callback(state);
            return Task.FromResult(0);
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }


        public bool InUse
        {
            get { throw new NotImplementedException(); }
        }

        public void MarkUsed(bool isUsed)
        {
            throw new NotImplementedException();
        }


        public void CountdownToClose(uint interval)
        {
            throw new NotImplementedException();
        }


        public int MaxCloseAttempts
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


        public int CloseAttempts
        {
            get { throw new NotImplementedException(); }
        }

        public bool IsDisposed
        {
            get { throw new NotImplementedException(); }
        }


        public bool HasShutdown
        {
            get { throw new NotImplementedException(); }
        }


        public void Authenticate()
        {
            throw new NotImplementedException();
        }

	    public bool CheckedForEnhancedAuthentication { get; set; }
	    public bool MustEnableServerFeatures { get; set; }

	    public bool IsConnected
        {
            get { return true; }
        }

        public DateTime? LastActivity { get; set; }
    }
}