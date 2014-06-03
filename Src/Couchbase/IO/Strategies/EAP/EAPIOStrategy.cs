using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;
using Common.Logging;
using Couchbase.Authentication.SASL;
using Couchbase.IO.Operations;
using Couchbase.IO.Strategies.Awaitable;
using Couchbase.IO.Utils;

namespace Couchbase.IO.Strategies.EAP
{
    internal class EapioStrategy : IOStrategy
    {
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();
        private readonly IConnectionPool _connectionPool;
        private static readonly AutoResetEvent SendEvent = new AutoResetEvent(false);
        private static readonly AutoResetEvent ReceiveEvent = new AutoResetEvent(false);

        public EapioStrategy(IConnectionPool connectionPool)
        {
            _connectionPool = connectionPool;
        }

        public IOperationResult<T> Execute<T>(IOperation<T> operation, IConnection connection)
        {
            throw new NotImplementedException();
        }

        private OperationAsyncState _state;
        public IOperationResult<T> Execute<T>(IOperation<T> operation)
        {
            var connection = _connectionPool.Acquire();

            if (_state == null)
            {
                _state = new OperationAsyncState
                {
                    Connection = connection
                };
            }
            var state = _state;

            if (state.Stream == null)
            {
                var ns = new NetworkStream(connection.Socket);
                var ssls = new SslStream(ns);
                ssls.AuthenticateAsClient(EndPoint.Address.ToString());
                state.Stream = ssls;
            }

            var buffer = operation.GetBuffer();
            state.Stream.BeginWrite(buffer, 0, buffer.Length, SendCallback, state);
            SendEvent.WaitOne();
            
            state.Stream.BeginRead(state.Buffer, 0, buffer.Length, ReceiveCallback, state);
            ReceiveEvent.WaitOne();

            CreateBody(state);
            operation.Header = state.Header;
            operation.Body = state.Body;

            _connectionPool.Release(connection);
            return operation.GetResult();
        }

        static void SendCallback(IAsyncResult asyncResult)
        {
            var state = asyncResult.AsyncState as OperationAsyncState;
            if (state == null)
            {
                throw new NullReferenceException("state cannot be null.");
            }

            state.Stream.EndWrite(asyncResult);
            SendEvent.Set();
            Log.Debug(m=>m("Bytes sent {0}", 0));
        }

        static void ReceiveCallback(IAsyncResult asyncResult)
        {
            var state = asyncResult.AsyncState as OperationAsyncState;
            if (state == null)
            {
                throw new NullReferenceException("state cannot be null.");
            }

            var bytesRead = state.Stream.EndRead(asyncResult);
            state.BytesReceived += bytesRead;
            Log.Debug(m => m("Bytes read {0}", state.BytesReceived));
            
            state.Data.Write(state.Buffer, 0, bytesRead);

            if (state.Header.BodyLength == 0)
            {
                CreateHeader(state);
                Log.Debug(m => m("received key {0}", state.Header.Key));
            }

            if (state.BytesReceived > 0 && state.BytesReceived < state.Header.TotalLength)
            {
                state.Stream.BeginRead(state.Buffer, 0, state.Buffer.Length, ReceiveCallback, state);
            }
            else
            {
                ReceiveEvent.Set();
            }
        }

        private static void CreateHeader(OperationAsyncState state)
        {
            var buffer = state.Data.GetBuffer();
            if (buffer.Length > 0)
            {
                state.Header = new OperationHeader
                {
                    Magic = buffer[HeaderIndexFor.Magic],
                    OperationCode = buffer[HeaderIndexFor.Opcode].ToOpCode(),
                    KeyLength = buffer.GetInt16(HeaderIndexFor.KeyLength),
                    ExtrasLength = buffer[HeaderIndexFor.ExtrasLength],
                    Status = buffer.GetResponseStatus(HeaderIndexFor.Status),
                    BodyLength = buffer.GetInt32(HeaderIndexFor.Body),
                    Opaque = buffer.GetUInt32(HeaderIndexFor.Opaque),
                    Cas = buffer.GetUInt64(HeaderIndexFor.Cas)
                };
            }
        }

        private static void CreateBody(OperationAsyncState state)
        {
            var buffer = state.Data.GetBuffer();
            state.Body = new OperationBody
            {
                Extras = new ArraySegment<byte>(buffer, OperationBase<object>.HeaderLength, state.Header.ExtrasLength),
                Data = new ArraySegment<byte>(buffer, 28, state.Header.BodyLength),
            };
        }

        public IPEndPoint EndPoint
        {
            get { return _connectionPool.EndPoint; }
        }

        public IConnectionPool ConnectionPool
        {
            get { throw new NotImplementedException(); }
        }

        public ISaslMechanism SaslMechanism
        {
            set { throw new NotImplementedException(); }
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
