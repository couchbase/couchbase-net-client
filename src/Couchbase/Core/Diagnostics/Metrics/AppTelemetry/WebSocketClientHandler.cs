using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Couchbase.Core.DI;
using Microsoft.Extensions.Logging;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Couchbase.Core.Compatibility;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Authentication.X509;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.Logging;
using Couchbase.Utils;

namespace Couchbase.Core.Diagnostics.Metrics.AppTelemetry;

#nullable enable

[InterfaceStability(Level.Volatile)]
internal class WebSocketClientHandler : IDisposable
{
    private const byte GetTelemetryOpcode = 0x00;
    private const byte SuccessOpcode = 0x00;
    private const byte ErrorOpcode = 0x01;
    private const int ReceiveBufferSize = 128;

    private ClientWebSocket? _webSocket;
    private readonly ILogger<WebSocketClientHandler> _logger;
    private readonly IAppTelemetryCollector _appTelemetryCollector;
    private readonly ICouchbaseHttpClientFactory _couchbaseHttpClientFactory;
    private readonly ICertificateValidationCallbackFactory _certificateValidationCallbackFactory;
    private readonly Redactor _redactor;
    private readonly Func<Uri, CancellationToken, Task> _runSession;
    private readonly TimeProvider _timeProvider;
    private int _attempt;
    private string? _pendingMetrics;
    private bool _warnedPlaintextOverride;
#if NET8_0_OR_GREATER
    private SocketsHttpHandler? _overrideHandler;
#endif

    // Released to wake the loop when the remote set changes.
    private readonly SemaphoreSlim _wakeSignal = new(0, 1);
    private volatile IReadOnlyList<Uri> _remotes = Array.Empty<Uri>();

    /// <summary>
    /// <paramref name="runSession"/> is a test seam that replaces connecting to a remote and receiving until closed.
    /// <paramref name="timeProvider"/> is a test seam that times the backoff. It defaults to the registered TimeProvider.
    /// </summary>
    public WebSocketClientHandler(IAppTelemetryCollector appTelemetryCollector,
        Func<Uri, CancellationToken, Task>? runSession = null,
        TimeProvider? timeProvider = null)
    {
        _appTelemetryCollector = appTelemetryCollector;
        _logger = _appTelemetryCollector.ClusterContext!.ServiceProvider
            .GetRequiredService<ILogger<WebSocketClientHandler>>();
        _couchbaseHttpClientFactory = _appTelemetryCollector.ClusterContext.ServiceProvider
            .GetRequiredService<ICouchbaseHttpClientFactory>();
        _redactor = _appTelemetryCollector.ClusterContext.ServiceProvider
            .GetRequiredService<Redactor>();
        _certificateValidationCallbackFactory = _appTelemetryCollector.ClusterContext.ServiceProvider
            .GetRequiredService<ICertificateValidationCallbackFactory>();
        _runSession = runSession ?? ConnectAndReceiveAsync;
        _timeProvider = timeProvider ?? _appTelemetryCollector.ClusterContext.ServiceProvider
            .GetRequiredService<TimeProvider>();
    }

    internal IReadOnlyList<Uri> Remotes => _remotes;

    /// <summary>
    /// Replaces the remotes that accept App Telemetry connections and wakes the loop.
    /// A running session stays open until the server closes it.
    /// </summary>
    public void UpdateRemotes(IReadOnlyList<Uri> remotes)
    {
        _remotes = remotes.ToList().Shuffle();

        // The collector serializes these calls, and the loop only takes the signal, so this cannot over-release.
        if (_wakeSignal.CurrentCount == 0)
        {
            _wakeSignal.Release();
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var remotes = _remotes;
                if (remotes.Count == 0)
                {
                    // Collection is paused, so an unsent payload must not reach a remote that appears later.
                    _pendingMetrics = null;
                    _logger.LogInformation("App Telemetry has no remotes available.");
                }

                var delay = remotes.Count == 0 ? Timeout.InfiniteTimeSpan : BackoffDelay(_attempt, remotes.Count);
                if (await WaitForRemoteChangeAsync(delay, cancellationToken).ConfigureAwait(false))
                {
                    // The remote set changed, so start a new pass without delay.
                    _attempt = 0;
                    continue;
                }

                var remote = remotes[_attempt % remotes.Count];
                _attempt++;
                await _runSession(remote, cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("App Telemetry connection to {Remote} closed.", _redactor.SystemData(remote));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("App Telemetry WebSocket connection failed: {Error}", ex.Message);
            }
        }

        _logger.LogDebug("App Telemetry reporter loop stopped.");
    }

    /// <summary>
    /// Waits up to <paramref name="delay"/> for the remote set to change. Returns false on timeout.
    /// </summary>
    private async Task<bool> WaitForRemoteChangeAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        if (delay == TimeSpan.Zero || delay == Timeout.InfiniteTimeSpan)
        {
            return await _wakeSignal.WaitAsync(delay, cancellationToken).ConfigureAwait(false);
        }

        // A timer cancels the wait. A cancelled wait leaves the semaphore, so it cannot take a later signal.
        using var timeout = new CancellationTokenSource();
        using var timer = _timeProvider.CreateTimer(CancelTimeout, timeout, delay, Timeout.InfiniteTimeSpan);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);
        try
        {
            await _wakeSignal.WaitAsync(linked.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private static void CancelTimeout(object? state)
    {
        try
        {
            ((CancellationTokenSource)state!).Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The timer can fire after the wait ended and disposed the source.
        }
    }

    private async Task ConnectAndReceiveAsync(Uri remote, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Initializing WebSocket connection to endpoint: {Endpoint}", _redactor.SystemData(remote));
        await InitializeWebSocketAsync(remote, cancellationToken).ConfigureAwait(false);

        if (_webSocket?.State == WebSocketState.Open)
        {
            // Reset the backoff to its first 100ms step, not zero, so a peer that closes at once is not hammered.
            _attempt = _remotes.Count;
            await ReceiveAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task InitializeWebSocketAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        _webSocket?.Dispose();
        _webSocket = CreateWebSocket(endpoint, out var handler);

#if NET8_0_OR_GREATER
        await _webSocket.ConnectAsync(endpoint, new HttpMessageInvoker(handler!), cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Successfully established WebSocket connection to {Endpoint}", _redactor.SystemData(endpoint));
#else
        await _webSocket.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
#endif
    }

    /// <summary>
    /// True when <paramref name="remote"/> is the endpoint the application configured, not a node the cluster advertised.
    /// The cluster's credentials belong to the cluster, so they must never be offered to an endpoint of the application's choosing.
    /// The collector captured the endpoint when it was created and keeps using it for its remotes, so this compares with that
    /// value. Changing the options afterwards must not turn the override into a cluster node.
    /// </summary>
    private bool IsOverrideEndpoint(Uri remote) =>
        _appTelemetryCollector.OverrideEndpoint is { } configured && configured == remote;

    /// <summary>
    /// Builds the socket for <paramref name="endpoint"/>. A cluster node is authenticated like any other HTTP request to
    /// the cluster. A configured override endpoint is only given the server certificate validation, never a password,
    /// token or client certificate (RFC 0084: the override endpoint is unauthenticated).
    /// </summary>
    /// <param name="endpoint">The remote the socket will connect to.</param>
    /// <param name="handler">On .NET 8 and later, the handler the connection must use. Always null on earlier targets.</param>
    internal ClientWebSocket CreateWebSocket(Uri endpoint, out HttpMessageHandler? handler)
    {
        var webSocket = new ClientWebSocket();
        webSocket.Options.KeepAliveInterval = _appTelemetryCollector.PingInterval;
        var isOverride = IsOverrideEndpoint(endpoint);

        if (isOverride && !_warnedPlaintextOverride && string.Equals(endpoint.Scheme, "ws", StringComparison.OrdinalIgnoreCase))
        {
            _warnedPlaintextOverride = true;
            _logger.LogWarning(
                "The App Telemetry endpoint {Endpoint} uses ws://, so telemetry is sent unencrypted. Use wss:// outside of development.",
                _redactor.SystemData(endpoint));
        }

#if NET8_0_OR_GREATER
        // If .NET >= 8, we can take a configured HttpMessageHandler instead of configuring the socket options.
        handler = isOverride ? GetOverrideHandler() : _couchbaseHttpClientFactory.Handler;
#else
        handler = null;

        // The previous WebSocket object does not take an HttpMessageInvoker (through which we pass the configured handler above)
        // We must therefore manually configure the ClientWebSocketOptions to match what would have been done
        // by the HttpClientHandler in the CouchbaseHttpClientFactory.
        // (Meaning configuring the RemoteCertificateValidationCallback, and client authentication via Password, Jwt or Client Certificates)
#if NET5_0_OR_GREATER
        var certValidationCallback = _certificateValidationCallbackFactory.CreateForHttp();
        webSocket.Options.RemoteCertificateValidationCallback = certValidationCallback;
#endif
#if !NETCOREAPP3_1_OR_GREATER
        _logger.LogDebug("This version of .NET does not support custom RemoteCertificateValidationCallback on the ClientWebSocketOptions");
#endif

        if (!isOverride)
        {
            _appTelemetryCollector.Authenticator!.AuthenticateClientWebSocket(webSocket);
        }
#endif

        return webSocket;
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// A handler that applies the cluster's TLS policy (server certificate validation, revocation, protocols and cipher
    /// suites) but carries no client certificate. It is kept for the life of this object so connections to the override endpoint can reuse it.
    /// </summary>
    private SocketsHttpHandler GetOverrideHandler()
    {
        if (_overrideHandler is not null) return _overrideHandler;

        var tls = _appTelemetryCollector.ClusterContext!.ClusterOptions.TlsSettings;
        var handler = new SocketsHttpHandler();
        handler.SslOptions.CertificateRevocationCheckMode = tls.EnableCertificateRevocation
            ? X509RevocationMode.Online
            : X509RevocationMode.NoCheck;
        handler.SslOptions.RemoteCertificateValidationCallback = _certificateValidationCallbackFactory.CreateForHttp();

        // The application's TLS restrictions apply to every endpoint it connects to, so only the credentials are left out.
        handler.SslOptions.EnabledSslProtocols = tls.EnabledSslProtocols;
        var clusterOptions = _appTelemetryCollector.ClusterContext.ClusterOptions;
        if (clusterOptions.PlatformSupportsCipherSuite && clusterOptions.EnabledTlsCipherSuites is { Count: > 0 })
        {
            handler.SslOptions.CipherSuitesPolicy = new CipherSuitesPolicy(clusterOptions.EnabledTlsCipherSuites);
        }

        return _overrideHandler = handler;
    }
#endif

    private async Task ReceiveAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[ReceiveBufferSize];
        var segment = new ArraySegment<byte>(buffer);
        _logger.LogDebug("Listening for AppTelemetry requests");
        while (_webSocket!.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var result = await _webSocket.ReceiveAsync(segment, cancellationToken).ConfigureAwait(false);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken).ConfigureAwait(false);
                break;
            }

            await HandleMessage(buffer.AsSpan(0, result.Count).ToArray(), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task HandleMessage(byte[] message, CancellationToken cancellationToken)
    {
        if (message.Length == 0) return;

        var opcode = message[0];
        if (opcode == GetTelemetryOpcode)
        {
            _logger.LogDebug("Received GetTelemetry message");
            await SendTelemetryAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _logger.LogDebug("Received Unrecognized message");
            await SendUnrecognizedOpcodeResponseAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SendTelemetryAsync(CancellationToken cancellationToken)
    {
        string metrics;

        if (_pendingMetrics != null)
        {
            // Retry previously unsent metrics before exporting new ones.
            metrics = _pendingMetrics;
        }
        else if (_appTelemetryCollector.TryExportMetricsAndReset(out var newMetrics))
        {
            metrics = newMetrics;
        }
        else
        {
            metrics = string.Empty;
        }

        var metricsBytes = Encoding.UTF8.GetBytes(metrics);
        var response = new byte[1 + metricsBytes.Length];
        response[0] = SuccessOpcode;
        metricsBytes.CopyTo(response, 1);

        _logger.LogTrace("Sending AppTelemetry metrics {Metrics}", metrics);

        try
        {
            await _webSocket!.SendAsync(
                new ArraySegment<byte>(response),
                WebSocketMessageType.Binary,
                true,
                cancellationToken).ConfigureAwait(false);
            _pendingMetrics = null;
        }
        catch
        {
            _pendingMetrics = metrics;
            throw;
        }
    }

    private async Task SendUnrecognizedOpcodeResponseAsync(CancellationToken cancellationToken)
    {
        await _webSocket!.SendAsync(
            new ArraySegment<byte>([ErrorOpcode]),
            WebSocketMessageType.Binary,
            true,
            cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        // The collector cancels the loop token first. The wake signal is not disposed because the loop may still wait on it.
        _webSocket?.Dispose();
#if NET8_0_OR_GREATER
        _overrideHandler?.Dispose();
#endif
    }

    /// <summary>
    /// No delay during the first pass over the remotes. After that the delay starts at 100ms
    /// and doubles with each pass up to the configured backoff.
    /// </summary>
    private TimeSpan BackoffDelay(int attempt, int remoteCount)
    {
        if (attempt < remoteCount) return TimeSpan.Zero;

        // Cap at int.MaxValue milliseconds, which every timer and wait accepts.
        var maxBackoffMs = Math.Min(Math.Max(_appTelemetryCollector.Backoff.TotalMilliseconds, 100), int.MaxValue);
        // Clamp the exponent so the shift cannot overflow. 100ms * 2^30 is above the cap.
        var delayMs = Math.Min(100L << Math.Min(attempt / remoteCount - 1, 30), maxBackoffMs);
        var delay = TimeSpan.FromMilliseconds(delayMs);
        _logger.LogDebug("App Telemetry connection attempt {Attempt} waiting {Delay} before retry.", attempt, delay);
        return delay;
    }
}
