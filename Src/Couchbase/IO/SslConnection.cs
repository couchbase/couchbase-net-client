using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Couchbase.Core.Diagnostics;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.IO.Utils;

namespace Couchbase.IO
{
    internal class SslConnection : ConnectionBase
    {
        private readonly SslStream _sslStream;
        private volatile bool _timingEnabled;

        internal SslConnection(ConnectionPool<SslConnection> connectionPool, Socket socket, IByteConverter converter)
            : this(connectionPool, socket, new SslStream(new NetworkStream(socket), true, ServerCertificateValidationCallback), converter)
        {
        }

        internal SslConnection(ConnectionPool<SslConnection> connectionPool, Socket socket, SslStream sslStream, IByteConverter converter)
            : base(socket, converter)
        {
            ConnectionPool = connectionPool;
            _sslStream = sslStream;
            Configuration = ConnectionPool.Configuration;
            _timingEnabled = Configuration.EnableOperationTiming;
        }

        private static bool ServerCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            Log.Info(m => m("Validating certificate: {0}", sslPolicyErrors));
            return true;
        }

        public void Authenticate()
        {
            try
            {
                var targetHost = ConnectionPool.EndPoint.Address.ToString();
                Log.Warn(m => m("Starting SSL encryption on {0}", targetHost));
                _sslStream.AuthenticateAsClient(targetHost);
                IsSecure = true;
            }
            catch (AuthenticationException e)
            {
                Log.Error(e);
            }
        }

        public override byte[] Send(byte[] buffer)
        {
            var state = new SocketAsyncState
            {
                Data = new MemoryStream(),
                Opaque = Converter.ToUInt32(buffer, HeaderIndexFor.Opaque)
            };

            _sslStream.BeginWrite(buffer, 0, buffer.Length, SendCallback, state);
            if (!SendEvent.WaitOne(Configuration.SendTimeout))
            {
                //TODO refactor logic
                IsDead = true;
                const string msg =
                    "The connection has timed out while an operation was in flight. The default is 15000ms.";
                throw new IOException(msg);
            }

            return state.Data.ToArray();
        }

        public override void Send<T>(IOperation<T> operation)
        {
            try
            {
                _sslStream.BeginWrite(operation.WriteBuffer, 0, operation.WriteBuffer.Length, SendCallback, operation);
                if (!SendEvent.WaitOne(Configuration.SendTimeout))
                {
                    const string msg =
                        "The connection has timed out while an operation was in flight. The default is 15000ms.";
                    operation.HandleClientError(msg, ResponseStatus.ClientFailure);
                    IsDead = true;
                }
            }
            catch (Exception e)
            {
                HandleException(e, operation);
            }
        }

        private void SendCallback(IAsyncResult asyncResult)
        {
            var state = (SocketAsyncState)asyncResult.AsyncState;
            try
            {
                _sslStream.EndWrite(asyncResult);
                state.Buffer = BufferManager.TakeBuffer(1024);
                _sslStream.BeginRead(state.Buffer, 0, state.Buffer.Length, ReceiveCallback, state);
            }
            catch (Exception e)
            {
                if (state.Buffer != null)
                {
                    BufferManager.ReturnBuffer(state.Buffer);
                }
                throw;
            }
        }

        private void ReceiveCallback(IAsyncResult asyncResult)
        {
            var state = (SocketAsyncState)asyncResult.AsyncState;
            try
            {
                var bytesRecieved = _sslStream.EndRead(asyncResult);
                state.BytesReceived += bytesRecieved;
                if (state.BytesReceived == 0)
                {
                    BufferManager.ReturnBuffer(state.Buffer);
                    SendEvent.Set();
                    return;
                }
                if (state.BodyLength == 0)
                {
                    state.BodyLength = Converter.ToInt32(state.Buffer, HeaderIndexFor.Body);
                }

                state.Data.Write(state.Buffer, 0, bytesRecieved);

                if (state.BytesReceived < state.BodyLength + 24)
                {
                    _sslStream.BeginRead(state.Buffer, 0, state.Buffer.Length, ReceiveCallback, state);
                }
                else
                {
                    BufferManager.ReturnBuffer(state.Buffer);
                    SendEvent.Set();
                }
            }
            catch (Exception e)
            {
                if (state.Buffer != null)
                {
                    BufferManager.ReturnBuffer(state.Buffer);
                }
                throw;
            }
        }

        /// <summary>
        /// Shuts down, closes and disposes of the internal <see cref="Socket"/> instance.
        /// </summary>
        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!Disposed)
                {
                    if (Socket != null)
                    {
                        if (Socket.Connected)
                        {
                            Socket.Shutdown(SocketShutdown.Both);
                        }
                        Socket.Dispose();
                    }
                    if (_sslStream != null)
                    {
                        _sslStream.Dispose();
                    }
                    //call the bases dispose to cleanup the timer
                    base.Dispose();
                }
            }
            else
            {
                if (!Disposed)
                {
                    if (Socket != null)
                    {
                        Socket.Dispose();
                    }
                    if (_sslStream != null)
                    {
                        _sslStream.Dispose();
                    }
                    //call the bases dispose to cleanup the timer
                    base.Dispose();
                }
            }
            Disposed = true;
        }
#if DEBUG
        ~SslConnection()
        {
            Dispose(false);
        }
#endif
    }
}
