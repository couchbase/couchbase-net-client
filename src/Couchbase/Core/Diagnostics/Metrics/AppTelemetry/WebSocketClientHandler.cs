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
    private readonly Random _random = new();
    private int _attempt;
    private string? _pendingMetrics;

    // Released to wake the loop when the remote set changes. Guarded by _remotesLock.
    private readonly SemaphoreSlim _wakeSignal = new(0, 1);
    private readonly CancellationTokenSource _disposeTokenSource = new();
    private readonly object _remotesLock = new();
    // Written under _remotesLock. Volatile so the getters can read without it.
    private volatile IReadOnlyList<Uri> _remotes = Array.Empty<Uri>();
    private volatile Uri? _selectedRemote;
    private CancellationTokenSource? _sessionTokenSource;
    private volatile bool _disposed;

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

    internal Uri? SelectedRemote => _selectedRemote;

    /// <summary>
    /// Replaces the set of remotes that accept App Telemetry connections.
    /// Disconnects from the current remote if it is no longer in the set.
    /// </summary>
    public void UpdateRemotes(IReadOnlyList<Uri> remotes)
    {
        CancellationTokenSource? sessionToCancel = null;
        lock (_remotesLock)
        {
            if (_disposed || remotes.SequenceEqual(_remotes)) return;

            _remotes = remotes.ToArray();

            if (_selectedRemote is null)
            {
                if (_remotes.Count > 0)
                {
                    WakeLocked();
                }
            }
            else if (!_remotes.Contains(_selectedRemote))
            {
                _logger.LogInformation(
                    "App Telemetry remote {Remote} no longer accepts telemetry. Disconnecting.",
                    _redactor.SystemData(_selectedRemote));
                _selectedRemote = null;
                sessionToCancel = _sessionTokenSource;
                WakeLocked();
            }
        }

        // Cancel outside the lock because cancellation callbacks can run inline.
        CancelQuietly(sessionToCancel);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource loopTokenSource;
        lock (_remotesLock)
        {
            if (_disposed) return;
            loopTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeTokenSource.Token);
        }

        using (loopTokenSource)
        {
            var loopToken = loopTokenSource.Token;
            while (!loopToken.IsCancellationRequested)
            {
                CancellationTokenSource? sessionTokenSource = null;
                try
                {
                    if (await WaitForBackoffAsync(_attempt++, loopToken).ConfigureAwait(false))
                    {
                        // Woken because the remote set changed, so retry without delay.
                        _attempt = 0;
                    }

                    var remote = SelectRemote(loopToken, out sessionTokenSource);
                    if (remote is null)
                    {
                        // Nothing to connect to. Sleep until the remote set changes.
                        await _wakeSignal.WaitAsync(loopToken).ConfigureAwait(false);
                        _attempt = 0;
                        continue;
                    }

                    await _runSession(remote, sessionTokenSource!.Token).ConfigureAwait(false);
                    _logger.LogDebug("App Telemetry connection to {Remote} closed.", _redactor.SystemData(remote));
                }
                catch (OperationCanceledException) when (loopToken.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException) when (_disposed)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("App Telemetry WebSocket connection failed: {Error}", ex.Message);
                }
                finally
                {
                    if (sessionTokenSource is not null)
                    {
                        lock (_remotesLock)
                        {
                            if (ReferenceEquals(_sessionTokenSource, sessionTokenSource))
                            {
                                _sessionTokenSource = null;
                            }
                        }
                        sessionTokenSource.Dispose();
                    }
                }
            }
        }

        _logger.LogDebug("App Telemetry reporter loop stopped.");
    }

    private Uri? SelectRemote(CancellationToken loopToken, out CancellationTokenSource? sessionTokenSource)
    {
        lock (_remotesLock)
        {
            sessionTokenSource = null;
            _selectedRemote = _remotes.RandomOrDefault();
            if (_selectedRemote is null)
            {
                _logger.LogInformation("App Telemetry has no remotes available.");
                return null;
            }

            _logger.LogInformation("Selected App Telemetry remote {Remote}.", _redactor.SystemData(_selectedRemote));
            sessionTokenSource = CancellationTokenSource.CreateLinkedTokenSource(loopToken);
            _sessionTokenSource = sessionTokenSource;
            return _selectedRemote;
        }
    }

    private void WakeLocked()
    {
        if (_wakeSignal.CurrentCount == 0)
        {
            _wakeSignal.Release();
        }
    }

    private static void CancelQuietly(CancellationTokenSource? tokenSource)
    {
        try
        {
            tokenSource?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The session already ended.
        }
    }

    private async Task ConnectAndReceiveAsync(Uri remote, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Initializing WebSocket connection to endpoint: {Endpoint}", _redactor.SystemData(remote));
        await InitializeWebSocketAsync(remote, cancellationToken).ConfigureAwait(false);

        if (_webSocket?.State == WebSocketState.Open)
        {
            // Start at the first backoff step, not zero, so a peer that closes right away is not hammered.
            _attempt = 1;
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
        CancellationTokenSource? sessionToCancel;
        lock (_remotesLock)
        {
            if (_disposed) return;
            _disposed = true;
            sessionToCancel = _sessionTokenSource;
        }

        _disposeTokenSource.Cancel();
        CancelQuietly(sessionToCancel);
        _webSocket?.Dispose();
        _wakeSignal.Dispose();
        _disposeTokenSource.Dispose();
    }

    /// <summary>
    /// Waits for the backoff delay of the given attempt. Returns true if woken early by a remote set change.
    /// Attempt 0 has no delay. Later attempts grow exponentially from 100ms up to the configured
    /// backoff, with full jitter.
    /// </summary>
    private Task<bool> WaitForBackoffAsync(int attempt, CancellationToken cancellationToken)
    {
        var delay = TimeSpan.Zero;
        if (attempt > 0)
        {
            // SemaphoreSlim.WaitAsync accepts at most int.MaxValue milliseconds.
            var maxBackoffMs = Math.Min(Math.Max(_appTelemetryCollector.Backoff.TotalMilliseconds, 100), int.MaxValue);
            // The cap is below 100ms * 2^25, so the exponent limit only prevents overflow.
            var delayMs = Math.Min(100L << Math.Min(attempt - 1, 30), maxBackoffMs);
            delay = TimeSpan.FromMilliseconds(delayMs * _random.NextDouble());
            _logger.LogDebug("App Telemetry connection attempt {Attempt} waiting {Delay} before retry.", attempt, delay);
        }

        return _wakeSignal.WaitAsync(delay, cancellationToken);
    }
}
