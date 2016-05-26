using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.IO.Utils;
using Couchbase.Utils;

namespace Couchbase.IO
{
    internal class SslConnection : ConnectionBase
    {
        private readonly SslStream _sslStream;
        private volatile bool _timingEnabled;

        public SslConnection(IConnectionPool connectionPool, Socket socket, IByteConverter converter)
            : this(connectionPool, socket, new SslStream(new NetworkStream(socket), true, ServerCertificateValidationCallback), converter)
        {
        }

        public SslConnection(IConnectionPool connectionPool, Socket socket, SslStream sslStream, IByteConverter converter)
            : base(socket, converter)
        {
            ConnectionPool = connectionPool;
            _sslStream = sslStream;
            Configuration = ConnectionPool.Configuration;
            _timingEnabled = Configuration.EnableOperationTiming;
        }

        private static bool ServerCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            Log.Info(m => m("Validating certificate [IgnoreRemoteCertificateNameMismatch={0}]: {1}", ClientConfiguration.IgnoreRemoteCertificateNameMismatch, sslPolicyErrors));

            if (ClientConfiguration.IgnoreRemoteCertificateNameMismatch)
            {
                if (sslPolicyErrors == SslPolicyErrors.RemoteCertificateNameMismatch)
                {
                    return true;
                }
            }
            return sslPolicyErrors == SslPolicyErrors.None;
        }

        public override void Authenticate()
        {
            try
            {
                var targetHost = ConnectionPool.Configuration.Uri.Host;
                Log.Warn(m => m("Starting SSL encryption on {0}", targetHost));
                _sslStream.AuthenticateAsClient(targetHost);
                IsSecure = true;
            }
            catch (AuthenticationException e)
            {
                Log.Error(e);
            }
        }

        public override async void SendAsync(byte[] request, Func<SocketAsyncState, Task> callback)
        {
            ExceptionDispatchInfo capturedException = null;
            SocketAsyncState state = null;
            try
            {
                state = new SocketAsyncState
                {
                    Data = new MemoryStream(),
                    Opaque = Converter.ToUInt32(request, HeaderIndexFor.Opaque),
                    Buffer = request,
                    Completed = callback
                };

                await _sslStream.WriteAsync(request, 0, request.Length);

                state.Buffer = BufferManager.TakeBuffer(Configuration.BufferSize);
                state.BytesReceived = await _sslStream.ReadAsync(state.Buffer, 0, Configuration.BufferSize);

                //write the received buffer to the state obj
                await state.Data.WriteAsync(state.Buffer, 0, state.BytesReceived);

                state.BodyLength = Converter.ToInt32(state.Buffer, HeaderIndexFor.BodyLength);
                while (state.BytesReceived < state.BodyLength + 24)
                {
                    var bufferLength = state.Buffer.Length - state.BytesSent < Configuration.BufferSize
                        ? state.Buffer.Length - state.BytesSent
                        : Configuration.BufferSize;

                    state.BytesReceived += await _sslStream.ReadAsync(state.Buffer, 0, bufferLength);
                    await state.Data.WriteAsync(state.Buffer, 0, state.BytesReceived - (int)state.Data.Length);
                }
                await callback(state);
            }
            catch (Exception e)
            {
                IsDead = true;
                capturedException = ExceptionDispatchInfo.Capture(e);
            }
            finally
            {
                ConnectionPool.Release(this);
                if (state != null && state.Buffer != null)
                {
                    BufferManager.ReturnBuffer(state.Buffer);
                }
            }

            if (capturedException != null)
            {
                var sourceException = capturedException.SourceException;
                if (state == null)
                {
                    await callback(new SocketAsyncState
                    {
                        Exception = capturedException.SourceException,
                        Status = (sourceException is SocketException)
                            ? ResponseStatus.TransportFailure
                            : ResponseStatus.ClientFailure
                    });
                }
                else
                {
                    state.Exception = sourceException;
                    await state.Completed(state);
                    Log.Debug(sourceException);
                }
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
                var msg = ExceptionUtil.GetMessage(ExceptionUtil.RemoteHostTimeoutMsg, Configuration.SendTimeout);
                throw new RemoteHostTimeoutException(msg);
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
                    var msg = ExceptionUtil.GetMessage(ExceptionUtil.RemoteHostTimeoutMsg, Configuration.SendTimeout);
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
            catch (Exception)
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
                            Socket.Close(ConnectionPool.Configuration.ShutdownTimeout);
                        }
                        else
                        {
                            Socket.Close();
                            Socket.Dispose();
                        }
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
                        Socket.Close();
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
