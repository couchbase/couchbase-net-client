using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Connections;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.DI
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

                var whichTask = await Task.WhenAny(connectTask, Task.Delay(_clusterOptions.KvConnectTimeout, cancellationToken));

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

            socket.SetKeepAlives(_clusterOptions.EnableTcpKeepAlives, (uint) _clusterOptions.TcpKeepAliveTime.TotalMilliseconds,
                (uint) _clusterOptions.TcpKeepAliveInterval.TotalMilliseconds);

            if (_clusterOptions.EffectiveEnableTls)
            {
                return new SslConnection(null, socket, _sslLogger);
            }

            return new MultiplexingConnection(null, socket, _multiplexLogger);
        }
    }
}
