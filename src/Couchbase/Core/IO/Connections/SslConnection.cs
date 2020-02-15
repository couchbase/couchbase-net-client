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
        private readonly SslStream _sslStream;
        private readonly object _syncObj = new object();
        private volatile bool _disposed;
        private readonly byte[] _receiveBuffer = new byte[1024 * 16];

        public SslConnection(Socket socket, ILogger<SslConnection> logger)
        {
            Socket = socket ?? throw new ArgumentNullException(nameof(socket));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _sslStream = new SslStream(new NetworkStream(socket), true, ServerCertificateValidationCallback);

            LocalEndPoint = socket.LocalEndPoint;
            EndPoint = socket.RemoteEndPoint;

            ConnectionId = ConnectionIdProvider.GetNextId();
        }

        /// <inheritdoc />
        public ulong ConnectionId { get; }

        private Socket Socket { get; }

        /// <inheritdoc />
        public bool IsConnected => !IsDead && !_disposed;

        /// <inheritdoc />
        public EndPoint EndPoint { get;  }

        /// <inheritdoc />
        public EndPoint LocalEndPoint { get; }

        /// <inheritdoc />
        public bool IsAuthenticated { get; set; }

        /// <inheritdoc />
        public bool IsSecure => _sslStream.IsEncrypted;

        /// <inheritdoc />
        public bool IsDead { get; private set; }

        private static bool ServerCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // TODO: add callback validation
            return true;
        }

        /// <inheritdoc />
        public async Task SendAsync(ReadOnlyMemory<byte> request, Func<SocketAsyncState, Task> callback, ErrorMap? errorMap = null)
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

                UpdateLastActivity();
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

        private DateTime _lastActivity = DateTime.UtcNow;

        /// <inheritdoc />
        public TimeSpan IdleTime => DateTime.UtcNow - _lastActivity;

        private void UpdateLastActivity()
        {
            _lastActivity = DateTime.UtcNow;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            Close();
        }

        private void Close()
        {
            if (_disposed) return;

            lock (_syncObj)
            {
                if (_disposed) return;

                _disposed = true;
                IsDead = true;

                _sslStream?.Dispose();
                Socket.Dispose();
            }
            _disposed = true;
        }

        /// <inheritdoc />
        public ValueTask CloseAsync(TimeSpan timeout)
        {
            // TODO: Deal with multiplexing support on SslConnection
            Close();
            return default;
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
