using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Authentication.X509;
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

        public ConnectionFactory(ClusterOptions clusterOptions,
            IIpEndPointService ipEndPointService,
            ILogger<MultiplexingConnection> multiplexLogger,
            ILogger<SslConnection> sslLogger,
            IRedactor redactor)
        {
            _clusterOptions = clusterOptions ?? throw new ArgumentNullException(nameof(clusterOptions));
            _ipEndPointService = ipEndPointService ?? throw new ArgumentNullException(nameof(ipEndPointService));
            _multiplexLogger = multiplexLogger ?? throw new ArgumentNullException(nameof(multiplexLogger));
            _sslLogger = sslLogger ?? throw new ArgumentNullException(nameof(sslLogger));
            _redactor = redactor;
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
                //Check if were using x509 auth, if so fetch the certificates
                X509Certificate2Collection? certs = null;
                if (_clusterOptions.X509CertificateFactory != null)
                {
                    certs = _clusterOptions.X509CertificateFactory.GetCertificates();
                    if (certs == null || certs.Count == 0)
                        throw new AuthenticationException(
                            "No certificates matching the X509FindType and specified FindValue were found in the Certificate Store.");

                    if (_sslLogger.IsEnabled(LogLevel.Debug))
                    {
                        foreach (var cert in certs)
                            _sslLogger.LogDebug("Cert sent {cert.FriendlyName} - Thumbprint {cert.Thumbprint}",
                                cert.FriendlyName, cert.Thumbprint);

                        _sslLogger.LogDebug("Sending {certs.Count} certificates to the server.", certs.Count);
                    }
                }

                //The endpoint we are connecting to
                var targetHost = _clusterOptions.ForceIpAsTargetHost
                    ? endPoint.Address.ToString()
                    : hostEndpoint.Host;

                //create the sslstream with appropriate authentication
                RemoteCertificateValidationCallback? certValidationCallback = _clusterOptions.KvCertificateCallbackValidation;
                if (certValidationCallback == null)
                {
                    CallbackCreator callbackCreator = new CallbackCreator(_clusterOptions.KvIgnoreRemoteCertificateNameMismatch, _sslLogger, _redactor, certs);
                    certValidationCallback = (__sender,__certificate, __chain, __sslPolicyErrors) =>
                        callbackCreator.Callback(__sender, __certificate, __chain, __sslPolicyErrors);
                }

                var sslStream = new SslStream(new NetworkStream(socket, true), false,
                    certValidationCallback);

#if !NETCOREAPP3_1_OR_GREATER
                await sslStream.AuthenticateAsClientAsync(targetHost, certs,
                        _clusterOptions.EnabledSslProtocols,
                        _clusterOptions.EnableCertificateRevocation)
                    .ConfigureAwait(false);
#else
                SslClientAuthenticationOptions sslOptions = new SslClientAuthenticationOptions()
                {
                    TargetHost = targetHost,
                    ClientCertificates = certs,
                    EnabledSslProtocols = _clusterOptions.EnabledSslProtocols,
                    CertificateRevocationCheckMode = _clusterOptions.EnableCertificateRevocation ? X509RevocationMode.Online : X509RevocationMode.NoCheck
                };
                if (_clusterOptions.PlatformSupportsCipherSuite
                    && _clusterOptions.EnabledTlsCipherSuites != null
                    && _clusterOptions.EnabledTlsCipherSuites.Count > 0)
                {
                    sslOptions.CipherSuitesPolicy = new CipherSuitesPolicy(_clusterOptions.EnabledTlsCipherSuites);
                }

                await sslStream.AuthenticateAsClientAsync(sslOptions)
                    .ConfigureAwait(false);
#endif

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
