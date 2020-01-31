using System;
using System.Buffers;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Errors;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.Connections
{
    internal sealed class SslConnection : IConnection
    {
        private readonly ILogger<SslConnection> _logger;
        private const int DefaultBufferSize = 1024;
        private readonly SslStream _sslStream;
        private readonly object _syncObj = new object();
        private volatile bool Disposed;
        private readonly byte[] _receiveBuffer = new byte[1024 * 16];

        public SslConnection(IConnectionPool? connectionPool, Socket socket, ILogger<SslConnection> logger)
        {
            ConnectionPool = connectionPool;
            Socket = socket ?? throw new ArgumentNullException(nameof(socket));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _sslStream = new SslStream(new NetworkStream(socket), true, ServerCertificateValidationCallback);

            LocalEndPoint = socket.LocalEndPoint;
            EndPoint = socket.RemoteEndPoint;
        }

        public ulong ConnectionId { get; }
        public IConnectionPool? ConnectionPool { get; set; }
        public Socket Socket { get; set; }
        public bool IsConnected { get; }
        public EndPoint EndPoint { get; set; }
        public EndPoint LocalEndPoint { get; }
        public bool IsAuthenticated { get; set; }
        public bool IsSecure => true;
        public bool IsDead { get; set; }
        public bool InUse { get; private set; }
        public bool IsDisposed => Disposed;
        public bool HasShutdown { get; private set; }
        public bool CheckedForEnhancedAuthentication { get; set; }
        public bool MustEnableServerFeatures { get; set; }

        private static bool ServerCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // TODO: add callback validation
            return true;
        }

        public Task SendAsync(ReadOnlyMemory<byte> request, Func<SocketAsyncState, Task> callback)
        {
            return SendAsync(request, callback, null);
        }

        public async Task SendAsync(ReadOnlyMemory<byte> request, Func<SocketAsyncState, Task> callback, ErrorMap? errorMap)
        {
            ExceptionDispatchInfo? capturedException = null;
            SocketAsyncState? state = null;
            try
            {
                var opaque = ByteConverter.ToUInt32(request.Span.Slice(HeaderOffsets.Opaque));
                state = new SocketAsyncState
                {
                    Opaque = opaque,
                    EndPoint = (IPEndPoint) EndPoint,
                    ConnectionId = ConnectionId,
                    LocalEndpoint = LocalEndPoint.ToString()
                };

                if (!MemoryMarshal.TryGetArray(request, out var arraySegment))
                {
                    // Fallback in case we can't use the more efficient TryGetArray method
                    arraySegment = new ArraySegment<byte>(request.ToArray());
                }

                // write data to stream
                await _sslStream.WriteAsync(arraySegment.Array, 0, request.Length).ConfigureAwait(false);

                // wait for response
                var received = await _sslStream.ReadAsync(_receiveBuffer, 0, _receiveBuffer.Length).ConfigureAwait(false);
                var responseSize = ByteConverter.ToInt32(_receiveBuffer.AsSpan(HeaderOffsets.BodyLength)) + HeaderOffsets.HeaderLength;

                // create memory slice and copy first segment
                var response = MemoryPool<byte>.Shared.RentAndSlice(responseSize);
                _receiveBuffer.AsMemory(0, received).CopyTo(response.Memory);

                // append any further segments as required
                while (received < responseSize)
                {
                    var segmentLength = await _sslStream.ReadAsync(_receiveBuffer, 0, _receiveBuffer.Length).ConfigureAwait(false);
                    _receiveBuffer.AsMemory(0, segmentLength).CopyTo(response.Memory);
                    received += segmentLength;
                }

                // write response to state and complete callback
                state.SetData(response);
                await callback(state).ConfigureAwait(false);
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
                    }).ConfigureAwait(false);
                }
                else
                {
                    state.Exception = sourceException;
                    await state.Completed(state).ConfigureAwait(false);
                    _logger.LogDebug(sourceException, "");
                }
            }
        }

        public void MarkUsed(bool isUsed)
        {
            InUse = isUsed;
        }

        public DateTime? LastActivity { get; private set; }

        internal void UpdateLastActivity()
        {
            LastActivity = DateTime.UtcNow;
        }

        /// <summary>
        /// Shuts down, closes and disposes of the internal <see cref="Socket"/> instance.
        /// </summary>
        public void Dispose()
        {
            if (Disposed) return;
            Close();
        }

        public void Close()
        {
            if (Disposed) return;

            lock (_syncObj)
            {
                if (Disposed) return;

                Disposed = true;
                IsDead = true;
                MarkUsed(false);

                _sslStream?.Dispose();
            }
            Disposed = true;
        }
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
