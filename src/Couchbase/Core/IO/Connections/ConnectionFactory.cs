using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.Connections
{
    /// <summary>
    /// Default implementation for <see cref="IConnectionFactory"/>.
    /// </summary>
    internal class ConnectionFactory : IConnectionFactory
    {
        private readonly ClusterOptions _clusterOptions;
        private readonly ILogger<MultiplexingConnection> _multiplexLogger;
        private readonly ILogger<SslConnection> _sslLogger;

        public ConnectionFactory(ClusterOptions clusterOptions, ILogger<MultiplexingConnection> multiplexLogger, ILogger<SslConnection> sslLogger)
        {
            _clusterOptions = clusterOptions ?? throw new ArgumentNullException(nameof(clusterOptions));
            _multiplexLogger = multiplexLogger ?? throw new ArgumentNullException(nameof(multiplexLogger));
            _sslLogger = sslLogger ?? throw new ArgumentNullException(nameof(sslLogger));
        }

        /// <inheritdoc />
        public async Task<IConnection> CreateAndConnectAsync(IPEndPoint endPoint, CancellationToken cancellationToken = default)
        {
            var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                var connectTask = socket.ConnectAsync(endPoint);

                var whichTask = await Task.WhenAny(connectTask, Task.Delay(_clusterOptions.KvConnectTimeout, cancellationToken))
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

#if NETCOREAPP3_0
            _multiplexLogger.LogDebug("Setting TCP Keep-Alives using SocketOptions - enable keep-alives {EnableTcpKeepAlives}, retries {TcpKeepAliveRetryCount}, time {TcpKeepAliveTime}, interval {TcpKeepAliveInterval}.",
                _clusterOptions.EnableTcpKeepAlives, _clusterOptions.TcpKeepAliveRetryCount, _clusterOptions.TcpKeepAliveTime, _clusterOptions.TcpKeepAliveInterval);

            if (!socket.TryEnableKeepAlives(_clusterOptions.EnableTcpKeepAlives,
                _clusterOptions.TcpKeepAliveRetryCount,
                (int)_clusterOptions.TcpKeepAliveTime.TotalSeconds,
                (int)_clusterOptions.TcpKeepAliveInterval.TotalSeconds, out string setKeepAliveMessage)
            )
            {
                _multiplexLogger.LogWarning(setKeepAliveMessage);
            }
#else
            _multiplexLogger.LogDebug("Setting TCP Keep-Alives using Socket.IOControl - enable tcp keep-alives {EnableTcpKeepAlives}, time {TcpKeepAliveTime}, interval {TcpKeepAliveInterval}",
                _clusterOptions.EnableTcpKeepAlives, _clusterOptions.TcpKeepAliveTime, _clusterOptions.TcpKeepAliveInterval);

             if (!socket.TrySetKeepAlives(_clusterOptions.EnableTcpKeepAlives,
                 (uint) _clusterOptions.TcpKeepAliveTime.TotalMilliseconds,
                 (uint) _clusterOptions.TcpKeepAliveInterval.TotalMilliseconds, out string setKeepAliveMessage)
             )
             {
                 _multiplexLogger.LogWarning(setKeepAliveMessage);
             }
#endif

            if (_clusterOptions.EffectiveEnableTls)
            {
                var sslStream = new SslStream(new NetworkStream(socket, true), false,  _clusterOptions.ValidateCertificateCallback);

                // TODO: add callback validation
                await sslStream.AuthenticateAsClientAsync(endPoint.Address.ToString(), null,
                        SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12,
                        _clusterOptions.EnableCertificateRevocation)
                    .ConfigureAwait(false);

                return new SslConnection(sslStream, socket.LocalEndPoint, socket.RemoteEndPoint,
                    _sslLogger, _multiplexLogger);
            }

            return new MultiplexingConnection(socket, _multiplexLogger);
        }
    }
}
