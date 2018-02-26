using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations.Errors;
using Couchbase.IO.Utils;
using Couchbase.Utils;
using OpenTracing;

namespace Couchbase.IO
{
    public class SslConnection : ConnectionBase
    {
        private readonly SslStream _sslStream;

        public SslConnection(IConnectionPool connectionPool, Socket socket, IByteConverter converter, BufferAllocator allocator)
            : this(connectionPool, socket, new SslStream(new NetworkStream(socket), true, ServerCertificateValidationCallback), converter, allocator)
        {

        }

        public SslConnection(IConnectionPool connectionPool, Socket socket, SslStream sslStream, IByteConverter converter, BufferAllocator allocator)
            : base(socket, converter, allocator)
        {
            ConnectionPool = connectionPool;
            _sslStream = sslStream;
            Configuration = ConnectionPool.Configuration;
        }

        private static bool ServerCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            Log.Info("Validating certificate [IgnoreRemoteCertificateNameMismatch={0}]: {1}", ClientConfiguration.IgnoreRemoteCertificateNameMismatch, sslPolicyErrors);

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
                Log.Info("Starting SSL encryption on {0}", targetHost);

                using (new SynchronizationContextExclusion())
                {
                    if (ConnectionPool.Configuration.EnableCertificateAuthentication)
                    {
                        if (ConnectionPool.Configuration.CertificateFactory == null)
                        {
                            throw new NullConfigException("If BucketConfiguration.EnableCertificateAuthentication is true, CertificateFactory cannot be null.");
                        }
                        var certs = ConnectionPool.Configuration.CertificateFactory();
                        _sslStream.AuthenticateAsClientAsync(targetHost, certs, SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12, true).Wait();
                    }
                    else
                    {
                        _sslStream.AuthenticateAsClientAsync(targetHost).Wait();
                    }
                }

                IsSecure = _sslStream.IsAuthenticated && _sslStream.IsSigned && _sslStream.IsEncrypted;
                if (!IsSecure)
                {
                    throw new AuthenticationException(ExceptionUtil.SslAuthenticationFailed);
                }
            }
            catch (AggregateException exception)
            {
                foreach (var e in exception.InnerExceptions)
                {
                    Log.Error(e);
                }
                throw;
            }
        }

        public override async void SendAsync(byte[] request, Func<SocketAsyncState, Task> callback, ISpan span, ErrorMap errorMap)
        {
            ExceptionDispatchInfo capturedException = null;
            SocketAsyncState state = null;
            try
            {
                var opaque = Converter.ToUInt32(request, HeaderIndexFor.Opaque);
                state = new SocketAsyncState
                {
                    Data = MemoryStreamFactory.GetMemoryStream(),
                    Opaque = opaque,
                    Buffer = request,
                    Completed = callback,
                    DispatchSpan = span,
                    CorrelationId = CreateCorrelationId(opaque),
                    ErrorMap = errorMap
                };

                await _sslStream.WriteAsync(request, 0, request.Length).ContinueOnAnyContext();

                state.SetIOBuffer(BufferAllocator.GetBuffer());
                state.BytesReceived = await _sslStream.ReadAsync(state.Buffer, state.BufferOffset, state.BufferLength).ContinueOnAnyContext();

                //write the received buffer to the state obj
                await state.Data.WriteAsync(state.Buffer, state.BufferOffset, state.BytesReceived).ContinueOnAnyContext();

                state.BodyLength = Converter.ToInt32(state.Buffer, state.BufferOffset + HeaderIndexFor.BodyLength);
                while (state.BytesReceived < state.BodyLength + 24)
                {
                    var bufferLength = state.BufferLength - state.BytesSent < state.BufferLength
                        ? state.BufferLength - state.BytesSent
                        : state.BufferLength;

                    state.BytesReceived += await _sslStream.ReadAsync(state.Buffer, state.BufferOffset, bufferLength).ContinueOnAnyContext();
                    await state.Data.WriteAsync(state.Buffer, state.BufferOffset, state.BytesReceived - (int)state.Data.Length).ContinueOnAnyContext();
                }
                await callback(state).ContinueOnAnyContext();
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
                    }).ContinueOnAnyContext();
                }
                else
                {
                    state.Exception = sourceException;
                    await state.Completed(state).ContinueOnAnyContext();
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
                        var opaque = Converter.ToUInt32(buffer, HeaderIndexFor.Opaque);
                        throw CreateTimeoutException(opaque);
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

            var opaque = Converter.ToUInt32(buffer, HeaderIndexFor.Opaque);
            var state = new SocketAsyncState
            {
                Data = MemoryStreamFactory.GetMemoryStream(),
                Opaque = opaque,
                CorrelationId = CreateCorrelationId(opaque)
            };

            await _sslStream.WriteAsync(buffer, 0, buffer.Length, cancellationToken).ContinueOnAnyContext();

            try
            {
                state.SetIOBuffer(BufferAllocator.GetBuffer());

                while (state.BytesReceived < state.BodyLength + 24)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var bytesReceived = await _sslStream.ReadAsync(state.Buffer, state.BufferOffset, state.BufferLength, cancellationToken).ContinueOnAnyContext();
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

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion
