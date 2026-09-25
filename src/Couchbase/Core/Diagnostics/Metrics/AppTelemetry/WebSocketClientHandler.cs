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
    private readonly IRedactor _redactor;
    private readonly Func<Uri, CancellationToken, Task> _runSession;
    private int _attempt;
    private string? _pendingMetrics;

    // Released to wake the loop when the remote set changes.
    private readonly SemaphoreSlim _wakeSignal = new(0, 1);
    private volatile IReadOnlyList<Uri> _remotes = Array.Empty<Uri>();

    /// <summary>
    /// <paramref name="runSession"/> is a test seam that replaces connecting to a remote and receiving until closed.
    /// </summary>
    public WebSocketClientHandler(IAppTelemetryCollector appTelemetryCollector,
        Func<Uri, CancellationToken, Task>? runSession = null)
    {
        _appTelemetryCollector = appTelemetryCollector;
        _logger = _appTelemetryCollector.ClusterContext!.ServiceProvider
            .GetRequiredService<ILogger<WebSocketClientHandler>>();
        _couchbaseHttpClientFactory = _appTelemetryCollector.ClusterContext.ServiceProvider
            .GetRequiredService<ICouchbaseHttpClientFactory>();
        _redactor = _appTelemetryCollector.ClusterContext.ServiceProvider
            .GetRequiredService<IRedactor>();
        _certificateValidationCallbackFactory = _appTelemetryCollector.ClusterContext.ServiceProvider
            .GetRequiredService<ICertificateValidationCallbackFactory>();
        _runSession = runSession ?? ConnectAndReceiveAsync;
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
                if (await _wakeSignal.WaitAsync(delay, cancellationToken).ConfigureAwait(false))
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
        _webSocket = new ClientWebSocket();
        _webSocket.Options.KeepAliveInterval = _appTelemetryCollector.PingInterval;

        // If .NET >= 8, we can simply take the configured HttpMessageHandler from the CouchbaseHttpClientFactory
        // which is already properly configured with the given Authenticator.
#if NET8_0_OR_GREATER
        var handler = _couchbaseHttpClientFactory.Handler;
        await _webSocket.ConnectAsync(endpoint, new HttpMessageInvoker(handler), cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Successfully established WebSocket connection to {Endpoint}", _redactor.SystemData(endpoint));
#else
        // The previous WebSocket object does not take an HttpMessageInvoker (through which we pass the configured handler above)
        // We must therefore manually configure the ClientWebSocketOptions to match what would have been done
        // by the HttpClientHandler in the CouchbaseHttpClientFactory.
        // (Meaning configuring the RemoteCertificateValidationCallback, and client authentication via Password, Jwt or Client Certificates)
#if NET5_0_OR_GREATER
            var certValidationCallback = _certificateValidationCallbackFactory.CreateForHttp();
            _webSocket.Options.RemoteCertificateValidationCallback = certValidationCallback;
#endif
#if !NETCOREAPP3_1_OR_GREATER
            _logger.LogDebug("This version of .NET does not support custom RemoteCertificateValidationCallback on the ClientWebSocketOptions");
#endif

        _appTelemetryCollector.Authenticator!.AuthenticateClientWebSocket(_webSocket);

        await _webSocket.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
#endif

    }

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
    }

    /// <summary>
    /// No delay during the first pass over the remotes. After that the delay starts at 100ms
    /// and doubles with each pass up to the configured backoff.
    /// </summary>
    private TimeSpan BackoffDelay(int attempt, int remoteCount)
    {
        if (attempt < remoteCount) return TimeSpan.Zero;

        // SemaphoreSlim.WaitAsync accepts at most int.MaxValue milliseconds.
        var maxBackoffMs = Math.Min(Math.Max(_appTelemetryCollector.Backoff.TotalMilliseconds, 100), int.MaxValue);
        // Clamp the exponent so the shift cannot overflow. 100ms * 2^30 is above the cap.
        var delayMs = Math.Min(100L << Math.Min(attempt / remoteCount - 1, 30), maxBackoffMs);
        var delay = TimeSpan.FromMilliseconds(delayMs);
        _logger.LogDebug("App Telemetry connection attempt {Attempt} waiting {Delay} before retry.", attempt, delay);
        return delay;
    }
}
