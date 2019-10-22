using System;
using System.IO;
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
using Couchbase.IO.Operations.Errors;
using Couchbase.IO.Utils;
using Couchbase.Utils;
using OpenTracing;

namespace Couchbase.IO
{
    public class SslConnection : ConnectionBase
    {
        private readonly SslStream _sslStream;
        private readonly IOBuffer _buffer;

        public SslConnection(IConnectionPool connectionPool, Socket socket, IByteConverter converter, BufferAllocator allocator)
            : this(connectionPool,
                socket,
                new SslStream(new NetworkStream(socket), true, GetCertificateCallback(connectionPool.Configuration.ClientConfiguration)),
                converter,
                allocator)
        {
        }

        public SslConnection(IConnectionPool connectionPool, Socket socket, SslStream sslStream, IByteConverter converter, BufferAllocator allocator)
            : base(socket, converter, allocator)
        {
            ConnectionPool = connectionPool;
            _sslStream = sslStream;
            Configuration = ConnectionPool.Configuration;
            _buffer = new IOBuffer(new byte[Configuration.BufferSize], 0, Configuration.BufferSize);
        }

        private static RemoteCertificateValidationCallback GetCertificateCallback(ClientConfiguration config)
        {
            if(config.KvServerCertificateValidationCallback == null)
            {
                return ServerCertificateValidationCallback;
            }
            return config.KvServerCertificateValidationCallback;
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
                    if (Configuration.ClientConfiguration.EnableCertificateAuthentication)
                    {
                        if (Configuration.ClientConfiguration.CertificateFactory == null)
                        {
                            throw new NullConfigException("If BucketConfiguration.EnableCertificateAuthentication is true, CertificateFactory cannot be null.");
                        }
                        var certs = Configuration.ClientConfiguration.CertificateFactory();
                        if (certs == null || certs.Count == 0)
                        {
                            throw new AuthenticationException("No certificates matching the X509FindType and specified FindValue were found in the Certificate Store.");
                        }
                        if (Log.IsDebugEnabled)
                        {
                            foreach (var cert in certs)
                            {
                                Log.Debug("Cert sent {0} - Thumbprint {1}", cert.FriendlyName, cert.Thumbprint);
                            }
                        }

                        Log.Debug("Sending {0} certificates to the server.", certs?.Count ?? 0);
                        _sslStream.AuthenticateAsClientAsync(targetHost,
                            certs,
                            SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12,
                            Configuration.ClientConfiguration.EnableCertificateRevocation).Wait();
                    }
                    else
                    {
                        _sslStream.AuthenticateAsClientAsync(targetHost).Wait();
                    }
                }

                IsSecure = _sslStream.IsAuthenticated && _sslStream.IsSigned && _sslStream.IsEncrypted;
                Log.Debug("IsAuthenticated {0} on {1}", _sslStream.IsAuthenticated, targetHost);
                Log.Debug("IsSigned {0} on {1}", _sslStream.IsSigned, targetHost);
                Log.Debug("IsEncrypted {0} on {1}", _sslStream.IsEncrypted, targetHost);

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

        public override async Task SendAsync(byte[] request, Func<SocketAsyncState, Task> callback, ISpan span, ErrorMap errorMap)
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
                    ConnectionId = ContextId,
                    LocalEndpoint = LocalEndPoint.ToString(),
                    ErrorMap = errorMap,
                    Timeout = Configuration.SendTimeout
                };

                await _sslStream.WriteAsync(request, 0, request.Length).ContinueOnAnyContext();

                state.SetIOBuffer(_buffer);
                state.BytesReceived = await _sslStream.ReadAsync(state.Buffer, state.BufferOffset, state.BufferLength).ContinueOnAnyContext();

                //write the received buffer to the state obj
                await state.Data.WriteAsync(state.Buffer, state.BufferOffset, state.BytesReceived).ContinueOnAnyContext();

                state.BodyLength = Converter.ToInt32(state.Buffer, state.BufferOffset + HeaderIndexFor.BodyLength);
                while (state.BytesReceived < state.BodyLength + OperationHeader.Length)
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

        public override byte[] Send(byte[] request)
        {
            try
            {
                var opaque = Converter.ToUInt32(request, HeaderIndexFor.Opaque);
                var state = new SocketAsyncState
                {
                    Data = MemoryStreamFactory.GetMemoryStream(),
                    Opaque = opaque,
                    ConnectionId = ContextId,
                    LocalEndpoint = LocalEndPoint.ToString(),
                    Timeout = Configuration.SendTimeout
                };

                _sslStream.Write(request, 0, request.Length);

                state.SetIOBuffer(_buffer);
                state.BytesReceived = _sslStream.Read(state.Buffer, state.BufferOffset, state.BufferLength);

                //write the received buffer to the state obj
                state.Data.Write(state.Buffer, state.BufferOffset, state.BytesReceived);

                state.BodyLength = Converter.ToInt32(state.Buffer, state.BufferOffset + HeaderIndexFor.BodyLength);
                while (state.BytesReceived < state.BodyLength + OperationHeader.Length)
                {
                    var bufferLength = state.BufferLength - state.BytesSent < state.BufferLength
                        ? state.BufferLength - state.BytesSent
                        : state.BufferLength;

                    state.BytesReceived += _sslStream.Read(state.Buffer, state.BufferOffset, bufferLength);
                    state.Data.Write(state.Buffer, state.BufferOffset, state.BytesReceived - (int)state.Data.Length);
                }

                return state.Data.ToArray();
            }
            catch (Exception)
            {
                IsDead = true;
                throw;
            }
        }

        /// <summary>
        /// Shuts down, closes and disposes of the internal <see cref="Socket"/> instance.
        /// </summary>
        public override void Dispose()
        {
            Dispose(true);
#if DEBUG
            GC.SuppressFinalize(this);
#endif
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
