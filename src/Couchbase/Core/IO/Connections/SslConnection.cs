using System;
using System.Buffers;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Errors;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.Connections
{
    internal sealed class SslConnection : IConnection
    {
        private readonly ILogger<SslConnection> _logger;
        private readonly SslStream _sslStream;
        private readonly MultiplexingConnection _multiplexingConnection;

        public SslConnection(SslStream stream, EndPoint localEndPoint, EndPoint remoteEndPoint,
            ILogger<SslConnection> logger, ILogger<MultiplexingConnection> multiplexingLogger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _sslStream = stream ?? throw new ArgumentNullException(nameof(stream));
            _multiplexingConnection = new MultiplexingConnection(_sslStream, localEndPoint, remoteEndPoint,
                multiplexingLogger);
        }

        /// <inheritdoc />
        public ulong ConnectionId => _multiplexingConnection.ConnectionId;

        /// <inheritdoc />
        public bool IsConnected => _multiplexingConnection.IsConnected;

        /// <inheritdoc />
        public EndPoint EndPoint => _multiplexingConnection.EndPoint;

        /// <inheritdoc />
        public EndPoint LocalEndPoint => _multiplexingConnection.LocalEndPoint;

        /// <inheritdoc />
        public bool IsAuthenticated
        {
            get => _multiplexingConnection.IsAuthenticated;
            set => _multiplexingConnection.IsAuthenticated = value;
        }

        /// <inheritdoc />
        public bool IsSecure => _sslStream.IsEncrypted;

        /// <inheritdoc />
        public bool IsDead => _multiplexingConnection.IsDead;

        /// <inheritdoc />
        public TimeSpan IdleTime => _multiplexingConnection.IdleTime;

        /// <inheritdoc />
        public ServerFeatureSet ServerFeatures
        {
            get => _multiplexingConnection.ServerFeatures;
            set => _multiplexingConnection.ServerFeatures = value;
        }

        /// <inheritdoc />
        public Task SendAsync(ReadOnlyMemory<byte> request, IOperation operation,
            ErrorMap? errorMap = null) =>
            _multiplexingConnection.SendAsync(request, operation, errorMap);

        /// <inheritdoc />
        public void Dispose()
        {
            _multiplexingConnection.Dispose();
        }

        /// <inheritdoc />
        public ValueTask CloseAsync(TimeSpan timeout) => _multiplexingConnection.CloseAsync(timeout);

        /// <inheritdoc />
        public void AddTags(IInternalSpan span) => _multiplexingConnection.AddTags(span);
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
