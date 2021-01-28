using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Authentication.X509;
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
        private readonly ILogger<MultiplexingConnection> _multiplexLogger;
        private readonly ILogger<SslConnection> _sslLogger;

        public ConnectionFactory(ClusterOptions clusterOptions, ILogger<MultiplexingConnection> multiplexLogger,
            ILogger<SslConnection> sslLogger)
        {
            _clusterOptions = clusterOptions ?? throw new ArgumentNullException(nameof(clusterOptions));
            _multiplexLogger = multiplexLogger ?? throw new ArgumentNullException(nameof(multiplexLogger));
            _sslLogger = sslLogger ?? throw new ArgumentNullException(nameof(sslLogger));
        }

        /// <inheritdoc />
        public async Task<IConnection> CreateAndConnectAsync(IPEndPoint endPoint,
            CancellationToken cancellationToken = default)
        {
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

#if NETCOREAPP_GTE_3_0
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
                var sslStream = new SslStream(new NetworkStream(socket, true), false,
                    _clusterOptions.KvCertificateCallbackValidation);

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
                var targetHost = endPoint.Address.ToString();

                //create the sslstream with appropriate authentication
                await sslStream.AuthenticateAsClientAsync(targetHost, certs,
                        SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12,
                        _clusterOptions.EnableCertificateRevocation)
                    .ConfigureAwait(false);

                var isSecure = sslStream.IsAuthenticated && sslStream.IsSigned && sslStream.IsEncrypted;
                _sslLogger.LogDebug("IsAuthenticated {0} on {1}", sslStream.IsAuthenticated, targetHost);
                _sslLogger.LogDebug("IsSigned {0} on {1}", sslStream.IsSigned, targetHost);
                _sslLogger.LogDebug("IsEncrypted {0} on {1}", sslStream.IsEncrypted, targetHost);

                //punt if we cannot successfully authenticate
                if (!isSecure) throw new AuthenticationException($"The SSL/TLS connection could not be authenticated on [{targetHost}].");

                return new SslConnection(sslStream, socket.LocalEndPoint, socket.RemoteEndPoint,
                    _sslLogger, _multiplexLogger);
            }

            return new MultiplexingConnection(socket, _multiplexLogger);
        }
    }
}
