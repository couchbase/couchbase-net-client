using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.IO.Utils;
using Couchbase.Utils;

namespace Couchbase.IO
{
    public class SslConnection : ConnectionBase
    {
        private readonly SslStream _sslStream;
        private volatile bool _timingEnabled;

        public  SslConnection(IConnectionPool connectionPool, Socket socket, IByteConverter converter, BufferAllocator allocator)
            : this(connectionPool, socket, new SslStream(new NetworkStream(socket), true, ServerCertificateValidationCallback), converter, allocator)
        {

        }

        public SslConnection(IConnectionPool connectionPool, Socket socket, SslStream sslStream, IByteConverter converter, BufferAllocator allocator)
            : base(socket, converter, allocator)
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
                var targetHost = EndPoint.ToString().Split(':')[0];
                Log.Warn(m => m("Starting SSL encryption on {0}", targetHost));

                using (new SynchronizationContextExclusion())
                {
                    _sslStream.AuthenticateAsClientAsync(targetHost).Wait();
                }

                IsSecure = true;
            }
            catch (AggregateException e)
            {
                var authException = e.InnerExceptions.OfType<AuthenticationException>().FirstOrDefault();

                if (authException != null)
                {
                    Log.Error(authException);
                }
                else
                {
                    throw;
                }
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

                await _sslStream.WriteAsync(request, 0, request.Length).ConfigureAwait(false);

                state.SetIOBuffer(BufferAllocator.GetBuffer());
                state.BytesReceived = await _sslStream.ReadAsync(state.Buffer, state.BufferOffset, state.BufferLength).ConfigureAwait(false);

                //write the received buffer to the state obj
                await state.Data.WriteAsync(state.Buffer, state.BufferOffset, state.BytesReceived).ConfigureAwait(false);

                state.BodyLength = Converter.ToInt32(state.Buffer, state.BufferOffset + HeaderIndexFor.BodyLength);
                while (state.BytesReceived < state.BodyLength + 24)
                {
                    var bufferLength = state.BufferLength - state.BytesSent < state.BufferLength
                        ? state.BufferLength - state.BytesSent
                        : state.BufferLength;

                    state.BytesReceived += await _sslStream.ReadAsync(state.Buffer, state.BufferOffset, bufferLength).ConfigureAwait(false);
                    await state.Data.WriteAsync(state.Buffer, state.BufferOffset, state.BytesReceived - (int)state.Data.Length).ConfigureAwait(false);
                }
                await callback(state).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                IsDead = true;
                capturedException = ExceptionDispatchInfo.Capture(e);
            }
            finally
            {
                ConnectionPool.Release(this);
                if (state.IOBuffer != null)
                {
                    BufferAllocator.ReleaseBuffer(state.IOBuffer);
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
                    }).ConfigureAwait(false);
                }
                else
                {
                    state.Exception = sourceException;
                    await state.Completed(state).ConfigureAwait(false);
                    Log.Debug(sourceException);
                }
            }
        }

        public override byte[] Send(byte[] buffer)
        {
            using (new SynchronizationContextExclusion())
            {
                // Token will cancel automatically after timeout
                var cancellationTokenSource = new CancellationTokenSource(Configuration.SendTimeout);

                try
                {
                    var task = SendAsync(buffer, cancellationTokenSource.Token);

                    task.Wait(cancellationTokenSource.Token);

                    return task.Result;
                }
                catch (AggregateException ex)
                {
                    //TODO refactor logic
                    IsDead = true;

                    if (ex.InnerException is TaskCanceledException)
                    {
                        // Timeout expired and cancellation token source was triggered
                        var msg = ExceptionUtil.GetMessage(ExceptionUtil.RemoteHostTimeoutMsg, Configuration.SendTimeout);
                        throw new RemoteHostTimeoutException(msg);
                    }
                    else
                    {
                        // Rethrow the aggregate exception
                        throw;
                    }
                }
            }
        }

        private async Task<byte[]> SendAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var state = new SocketAsyncState
            {
                Data = new MemoryStream(),
                Opaque = Converter.ToUInt32(buffer, HeaderIndexFor.Opaque)
            };

            await _sslStream.WriteAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);

            try
            {
                state.SetIOBuffer(BufferAllocator.GetBuffer());

                while (state.BytesReceived < state.BodyLength + 24)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var bytesReceived = await _sslStream.ReadAsync(state.Buffer, state.BufferOffset, state.BufferLength, cancellationToken).ConfigureAwait(false);
                    state.BytesReceived += bytesReceived;

                    if (state.BytesReceived == 0)
                    {
                        // No more bytes were received, go ahead and exit the loop
                        break;
                    }
                    if (state.BodyLength == 0)
                    {
                        // Reading header, so get the BodyLength
                        state.BodyLength = Converter.ToInt32(state.Buffer, state.BufferOffset + HeaderIndexFor.Body);
                    }

                    state.Data.Write(state.Buffer, state.BufferOffset, bytesReceived);
                }
            }
            finally
            {
                if (state.IOBuffer != null)
                {
                    BufferAllocator.ReleaseBuffer(state.IOBuffer);
                }
            }

            return state.Data.ToArray();
        }

        public override void Send<T>(IOperation<T> operation)
        {
            throw new NotImplementedException();
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
