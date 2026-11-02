using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.Logging;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

namespace Couchbase.ConfigRecorder.Tap;

/// <summary>
/// The SDK ConnectionFactory with the same connect, keep-alive and TLS steps. It puts a tap between
/// the connection and its stream.
/// </summary>
internal sealed class TappingConnectionFactory : IConnectionFactory
{
    private readonly ClusterOptions _clusterOptions;
    private readonly IIpEndPointService _ipEndPointService;
    private readonly ILogger<MultiplexingConnection> _multiplexLogger;
    private readonly ILogger<SslConnection> _sslLogger;
    private readonly Redactor _redactor;
    private readonly ICertificateValidationCallbackFactory _callbackFactory;
    private readonly TapHub _hub;
    private readonly string _role;

    public TappingConnectionFactory(ClusterOptions clusterOptions,
        IIpEndPointService ipEndPointService,
        ILogger<MultiplexingConnection> multiplexLogger,
        ILogger<SslConnection> sslLogger,
        Redactor redactor,
        ICertificateValidationCallbackFactory callbackFactory,
        TapHub hub,
        string role)
    {
        _clusterOptions = clusterOptions;
        _ipEndPointService = ipEndPointService;
        _multiplexLogger = multiplexLogger;
        _sslLogger = sslLogger;
        _redactor = redactor;
        _callbackFactory = callbackFactory;
        _hub = hub;
        _role = role;
    }

    /// <summary>
    /// Registers the factory on the options, so every KV connection of that cluster goes through it.
    /// </summary>
    public static void Register(ClusterOptions options, TapHub hub, string role) =>
        options.AddClusterService<IConnectionFactory, TappingConnectionFactory>(sp => new TappingConnectionFactory(
            Get<ClusterOptions>(sp),
            Get<IIpEndPointService>(sp),
            Get<ILogger<MultiplexingConnection>>(sp),
            Get<ILogger<SslConnection>>(sp),
            Get<Redactor>(sp),
            Get<ICertificateValidationCallbackFactory>(sp),
            hub,
            role));

    private static T Get<T>(IServiceProvider sp) =>
        (T)(sp.GetService(typeof(T)) ?? throw new InvalidOperationException($"Service {typeof(T).Name} is not registered."));

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

        _multiplexLogger.LogDebug("Connecting to {Host} at {Address}.",
            _redactor.SystemData(hostEndpoint.Host), _redactor.SystemData(endPoint));

        var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            var connectTask = socket.ConnectAsync(endPoint);

            var whichTask = await Task
                .WhenAny(connectTask, Task.Delay(_clusterOptions.KvConnectTimeout, cancellationToken))
                .ConfigureAwait(false);

            if (whichTask != connectTask)
            {
                const int connectionTimedOut = 10060;
                throw new SocketException(connectionTimedOut);
            }
        }
        catch
        {
            socket.Dispose();
            throw;
        }

        if (!socket.TryEnableKeepAlives(_clusterOptions.EnableTcpKeepAlives,
                (int)_clusterOptions.TcpKeepAliveTime.TotalSeconds,
                (int)_clusterOptions.TcpKeepAliveInterval.TotalSeconds, out var setKeepAliveMessage))
        {
            _multiplexLogger.LogWarning(setKeepAliveMessage);
        }

        // Null when the connect failed. The stream constructor below then throws as in the SDK.
        var localEndPoint = socket.LocalEndPoint;
        var remoteEndPoint = socket.RemoteEndPoint;
        var tap = _hub.CreateTap(_role, hostEndpoint.Host, hostEndpoint.Port, _clusterOptions.EffectiveEnableTls,
            Format(remoteEndPoint), Format(localEndPoint));

        IConnection connection;
        if (_clusterOptions.EffectiveEnableTls)
        {
            var authenticator = _clusterOptions.GetEffectiveAuthenticator();

            var targetHost = _clusterOptions.ForceIpAsTargetHost
                ? endPoint.Address.ToString()
                : hostEndpoint.Host;

            var sslStream = new TappingSslStream(new NetworkStream(socket, true), tap);
            try
            {
                await authenticator.AuthenticateSslStream(sslStream, targetHost, _clusterOptions, _callbackFactory,
                    cancellationToken, _sslLogger).ConfigureAwait(false);

                var isSecure = sslStream.IsAuthenticated && sslStream.IsSigned && sslStream.IsEncrypted;
                if (!isSecure)
                {
                    throw new AuthenticationException($"The SSL/TLS connection could not be authenticated on [{targetHost}].");
                }
            }
            catch
            {
                await sslStream.DisposeAsync().ConfigureAwait(false);
                throw;
            }

            connection = new SslConnection(sslStream, _clusterOptions.Tuning.MaximumInFlightOperationsPerConnection,
                localEndPoint!, remoteEndPoint!, _sslLogger, _multiplexLogger, hostEndpoint.Host);
        }
        else
        {
            var stream = new TappingStream(new NetworkStream(socket, true), tap);
            connection = new MultiplexingConnection(stream, _clusterOptions.Tuning.MaximumInFlightOperationsPerConnection,
                localEndPoint!, remoteEndPoint!, _multiplexLogger, hostEndpoint.Host);
        }

        tap.Attach(connection.ConnectionId);
        return connection;
    }

    private static string Format(EndPoint? endPoint) =>
        endPoint is IPEndPoint ip ? $"{ip.Address}:{ip.Port}" : endPoint?.ToString() ?? "";
}
