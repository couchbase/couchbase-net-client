using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.Logging;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.Connections
{
    /// <summary>
    ///     Default implementation for <see cref="IConnectionFactory" />.
    /// </summary>
    internal class ConnectionFactory : IConnectionFactory
    {
        private readonly ClusterOptions _clusterOptions;
        private readonly IIpEndPointService _ipEndPointService;
        private readonly ILogger<MultiplexingConnection> _multiplexLogger;
        private readonly ILogger<SslConnection> _sslLogger;
        private readonly IRedactor _redactor;
        private readonly ICertificateValidationCallbackFactory _callbackFactory;

        public ConnectionFactory(ClusterOptions clusterOptions,
            IIpEndPointService ipEndPointService,
            ILogger<MultiplexingConnection> multiplexLogger,
            ILogger<SslConnection> sslLogger,
            IRedactor redactor,
            ICertificateValidationCallbackFactory callbackFactory)
        {
            _clusterOptions = clusterOptions ?? throw new ArgumentNullException(nameof(clusterOptions));
            _ipEndPointService = ipEndPointService ?? throw new ArgumentNullException(nameof(ipEndPointService));
            _multiplexLogger = multiplexLogger ?? throw new ArgumentNullException(nameof(multiplexLogger));
            _sslLogger = sslLogger ?? throw new ArgumentNullException(nameof(sslLogger));
            _redactor = redactor;
            _callbackFactory = callbackFactory ?? throw new ArgumentNullException(nameof(callbackFactory));
        }

        /// <inheritdoc />
        public async Task<IConnection> CreateAndConnectAsync(HostEndpointWithPort hostEndpoint,
            CancellationToken cancellationToken = default)
        {
            if (_clusterOptions.IsCapella && !_clusterOptions.EffectiveEnableTls)
            {
                _multiplexLogger.LogWarning("TLS is required when connecting to Couchbase Capella. Please enable TLS by prefixing the connection string with \"couchbases://\" (note the final 's').");
            }

            var endPoint = await _ipEndPointService.GetIpEndPointAsync(hostEndpoint.Host, hostEndpoint.Port, cancellationToken)
                .ConfigureAwait(false);
            if (endPoint is null)
            {
                throw new ConnectException($"Unable to resolve host '{hostEndpoint}'.");
            }

            var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                var connectTask = socket.ConnectAsync(endPoint);

                var whichTask = await Task
                    .WhenAny(connectTask, Task.Delay(_clusterOptions.KvConnectTimeout, cancellationToken))
                    .ConfigureAwait(false);

                if (whichTask != connectTask)
                {
                    // Was a timeout
                    const int connectionTimedOut = 10060;
                    throw new SocketException(connectionTimedOut);
                }
            }
            catch
            {
                socket.Dispose();
                throw;
            }

#if NETCOREAPP3_0_OR_GREATER
            _multiplexLogger.LogDebug("Setting TCP Keep-Alives using SocketOptions - enable keep-alives {EnableTcpKeepAlives}, time {TcpKeepAliveTime}, interval {TcpKeepAliveInterval}.",
                _clusterOptions.EnableTcpKeepAlives, _clusterOptions.TcpKeepAliveTime, _clusterOptions.TcpKeepAliveInterval);

            if (!socket.TryEnableKeepAlives(_clusterOptions.EnableTcpKeepAlives,
                (int)_clusterOptions.TcpKeepAliveTime.TotalSeconds,
                (int)_clusterOptions.TcpKeepAliveInterval.TotalSeconds, out string setKeepAliveMessage)
            )
            {
                _multiplexLogger.LogWarning(setKeepAliveMessage);
            }
#else
            _multiplexLogger.LogDebug(
                "Setting TCP Keep-Alives using Socket.IOControl on {endpoint} - enable tcp keep-alives {EnableTcpKeepAlives}, time {TcpKeepAliveTime}, interval {TcpKeepAliveInterval}",
                endPoint, _clusterOptions.EnableTcpKeepAlives, _clusterOptions.TcpKeepAliveTime,
                _clusterOptions.TcpKeepAliveInterval);

            if (!socket.TrySetKeepAlives(_clusterOptions.EnableTcpKeepAlives,
                (uint) _clusterOptions.TcpKeepAliveTime.TotalMilliseconds,
                (uint) _clusterOptions.TcpKeepAliveInterval.TotalMilliseconds, out var setKeepAliveMessage)
            )
                _multiplexLogger.LogWarning(setKeepAliveMessage);
#endif

            if (_clusterOptions.EffectiveEnableTls)
            {
                var authenticator = _clusterOptions.GetEffectiveAuthenticator();

                //The endpoint we are connecting to
                var targetHost = _clusterOptions.ForceIpAsTargetHost
                    ? endPoint.Address.ToString()
                    : hostEndpoint.Host;

                var sslStream = new SslStream(new NetworkStream(socket, true), false);

                await authenticator.AuthenticateSslStream(sslStream, targetHost, _clusterOptions, _callbackFactory, cancellationToken, _sslLogger).ConfigureAwait(false);

                var isSecure = sslStream.IsAuthenticated && sslStream.IsSigned && sslStream.IsEncrypted;
                _sslLogger.LogDebug("IsAuthenticated {0} on {1}", sslStream.IsAuthenticated, _redactor.SystemData(targetHost));
                _sslLogger.LogDebug("IsSigned {0} on {1}", sslStream.IsSigned, _redactor.SystemData(targetHost));
                _sslLogger.LogDebug("IsEncrypted {0} on {1}", sslStream.IsEncrypted, _redactor.SystemData(targetHost));

                //punt if we cannot successfully authenticate
                if (!isSecure) throw new AuthenticationException($"The SSL/TLS connection could not be authenticated on [{targetHost}].");

                return new SslConnection(sslStream, _clusterOptions.Tuning.MaximumInFlightOperationsPerConnection,
                    socket.LocalEndPoint!, socket.RemoteEndPoint!, _sslLogger, _multiplexLogger);
            }

            return new MultiplexingConnection(socket, _clusterOptions.Tuning.MaximumInFlightOperationsPerConnection, _multiplexLogger);
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
