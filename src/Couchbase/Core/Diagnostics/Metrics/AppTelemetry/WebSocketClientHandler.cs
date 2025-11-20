using System;
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
    private int _attempt = 0;
    private readonly int _clampedExponent = 0;
    private Uri? Endpoint => _appTelemetryCollector.Endpoint(_attempt);

    public WebSocketClientHandler(IAppTelemetryCollector appTelemetryCollector)
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
        //Cache the max exponent for the backoff
        _clampedExponent = (int)Math.Floor(Math.Log(_appTelemetryCollector.Backoff.TotalMilliseconds / 100.0, 2));
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_webSocket?.State != WebSocketState.Open)
                {
                    _logger.LogDebug("Initializing WebSocket connection to endpoint: {Endpoint}", Endpoint);
                    await InitializeWebSocketAsync(cancellationToken).ConfigureAwait(false);
                }

                if (_webSocket?.State == WebSocketState.Open)
                {
                    await ReceiveAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error connecting to AppTelemetry WebSocket endpoint {Endpoint} process: {Error}", Endpoint, ex.Message);
            }
            finally
            {
                await BackoffAsync().ConfigureAwait(false);
                _attempt++;
            }
        }
    }

    private async Task InitializeWebSocketAsync(CancellationToken cancellationToken)
    {
        if (Endpoint is null) return;

        _webSocket?.Dispose();
        _webSocket = new ClientWebSocket();
        _webSocket.Options.KeepAliveInterval = _appTelemetryCollector.PingInterval;

        // If .NET >= 8, we can simply take the configured HttpMessageHandler from the CouchbaseHttpClientFactory
        // which is already properly configured with the given Authenticator.
#if NET8_0_OR_GREATER
        var handler = _couchbaseHttpClientFactory.Handler;
        await _webSocket.ConnectAsync(Endpoint, new HttpMessageInvoker(handler), cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Successfully established WebSocket connection to {Endpoint}", Endpoint);
#else
        // The previous WebSocket object does not take an HttpMessageInvoker (through which we pass the configured handler above)
        // We must therefore manually configure the ClientWebSocketOptions to match what would have been done
        // by the HttpClientHandler in the CouchbaseHttpClientFactory.
        // (Meaning configuring the RemoteCertificateValidationCallback, and client authentication via Password or Client Certificates)
#if NET5_0_OR_GREATER
            var certValidationCallback = _certificateValidationCallbackFactory.CreateForHttp();
            _webSocket.Options.RemoteCertificateValidationCallback = certValidationCallback;
#endif
#if !NETCOREAPP3_1_OR_GREATER
            _logger.LogDebug("This version of .NET does not support custom RemoteCertificateValidationCallback on the ClientWebSocketOptions");
#endif

        _appTelemetryCollector.Authenticator!.AuthenticateClientWebSocket(_webSocket);

        await _webSocket.ConnectAsync(Endpoint, cancellationToken).ConfigureAwait(false);
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
        if (_appTelemetryCollector.TryExportMetricsAndReset(out var metrics))
        {
            var metricsBytes = Encoding.UTF8.GetBytes(metrics);
            var response = new byte[1 + metricsBytes.Length];
            response[0] = SuccessOpcode;
            metricsBytes.CopyTo(response, 1);

            _logger.LogDebug("Sending AppTelemetry metrics {Metrics}", metrics); //TODO: this is for debugging

            await _webSocket!.SendAsync(
                new ArraySegment<byte>(response),
                WebSocketMessageType.Binary,
                true,
                cancellationToken).ConfigureAwait(false);
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
        _webSocket?.Dispose();
    }

    /// <summary>
    /// Awaits with an exponentially increasing backoff delay starting at 100ms capped at clampedExponent.
    /// </summary>
    private async Task BackoffAsync()
    {
        var maxBackoff = _appTelemetryCollector.Backoff;

        if (maxBackoff <= TimeSpan.FromMilliseconds(100))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
            return;
        }
        //To prevent overflow, we clamp the exponent to a maximum value
        var clampedAttempt = Math.Min(_attempt, _clampedExponent);

        var delayMs = 100L << clampedAttempt; //100ms * 2^attempt
        var cappedDelayMs = Math.Min(delayMs, (long)maxBackoff.TotalMilliseconds);

        await Task.Delay(TimeSpan.FromMilliseconds(cappedDelayMs)).ConfigureAwait(false);
    }
}
