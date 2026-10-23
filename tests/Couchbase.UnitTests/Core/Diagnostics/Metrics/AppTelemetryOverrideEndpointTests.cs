using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Diagnostics.Metrics.AppTelemetry;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Couchbase.UnitTests.Core.Diagnostics.Metrics;

/// <summary>
/// The cluster's credentials must reach cluster nodes only, never an App Telemetry endpoint the application configured (NCBC-4337).
/// </summary>
[Collection("AppTelemetry")]
public class AppTelemetryOverrideEndpointTests
{
    private static readonly Uri Override = new("wss://telemetry.example.com/collect");
    private static readonly Uri ClusterNode = new("ws://127.0.0.1:8091/_appTelemetry");
    private const string JwtToken = "header.payload.signature";
    // Only bounds a wait for something the test expects, so a passing test never reaches it.
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public void Override_Endpoint_Is_Not_Given_The_Cluster_Credentials()
    {
        using var collector = CreateCollector(Override);
        using var handler = new WebSocketClientHandler(collector);
        using var socket = handler.CreateWebSocket(Override, out var connectionHandler);

#if NET8_0_OR_GREATER
        var shared = collector.ClusterContext!.ServiceProvider.GetRequiredService<ICouchbaseHttpClientFactory>().Handler;
        var sockets = Assert.IsType<SocketsHttpHandler>(connectionHandler);
        Assert.NotSame(shared, sockets);
        Assert.True(sockets.SslOptions.ClientCertificates is null or { Count: 0 });
        Assert.NotNull(sockets.SslOptions.RemoteCertificateValidationCallback);
#else
        Assert.Null(connectionHandler);
        Assert.Null(socket.Options.Credentials);
        Assert.Empty(socket.Options.ClientCertificates);
#endif
    }

    [Fact]
    public void Override_Endpoint_Handler_Is_Reused_Across_Connections()
    {
        using var collector = CreateCollector(Override);
        using var handler = new WebSocketClientHandler(collector);
        using var first = handler.CreateWebSocket(Override, out var firstHandler);
        using var second = handler.CreateWebSocket(Override, out var secondHandler);

        Assert.Same(firstHandler, secondHandler);
    }

    [Fact]
    public void Cluster_Node_Is_Still_Authenticated_When_An_Override_Is_Configured()
    {
        // The override replaces the advertised remotes, but a node URI must keep the cluster's authentication.
        using var collector = CreateCollector(Override);
        using var handler = new WebSocketClientHandler(collector);
        using var socket = handler.CreateWebSocket(ClusterNode, out var connectionHandler);

#if NET8_0_OR_GREATER
        var shared = collector.ClusterContext!.ServiceProvider.GetRequiredService<ICouchbaseHttpClientFactory>().Handler;
        Assert.Same(shared, connectionHandler);
#else
        Assert.Null(connectionHandler);
        var credentials = socket.Options.Credentials as System.Net.NetworkCredential;
        Assert.NotNull(credentials);
        Assert.Equal("username", credentials.UserName);
#endif
    }

    [Fact]
    public void Without_An_Override_Cluster_Nodes_Use_The_Cluster_Authentication()
    {
        using var collector = CreateCollector(null);
        using var handler = new WebSocketClientHandler(collector);
        using var socket = handler.CreateWebSocket(ClusterNode, out var connectionHandler);

#if NET8_0_OR_GREATER
        var shared = collector.ClusterContext!.ServiceProvider.GetRequiredService<ICouchbaseHttpClientFactory>().Handler;
        Assert.Same(shared, connectionHandler);
#else
        Assert.NotNull(socket.Options.Credentials);
#endif
    }

    [Fact]
    public void Plaintext_Override_Logs_A_Warning_Once()
    {
        var loggerFactory = new CountingLoggerFactory();
        var plaintext = new Uri("ws://sink.example.com/collect");
        using var collector = CreateCollector(plaintext, loggerFactory);
        using var handler = new WebSocketClientHandler(collector);

        using var first = handler.CreateWebSocket(plaintext, out _);
        using var second = handler.CreateWebSocket(plaintext, out _);

        Assert.Equal(1, loggerFactory.Count("unencrypted"));
    }

    [Fact]
    public void Secure_Override_Logs_No_Warning()
    {
        var loggerFactory = new CountingLoggerFactory();
        using var collector = CreateCollector(Override, loggerFactory);
        using var handler = new WebSocketClientHandler(collector);

        using var socket = handler.CreateWebSocket(Override, out _);

        Assert.Equal(0, loggerFactory.Count("unencrypted"));
    }

    // The tests above check how the socket is set up. These check the upgrade request that actually goes out.

    [Theory]
    [InlineData(AuthKind.Password)]
    [InlineData(AuthKind.Jwt)]
    public async Task Override_Endpoint_Receives_No_Authorization_Header(AuthKind auth)
    {
        using var listener = new LoopbackListener();
        using var collector = CreateCollector(listener.Remote, auth: auth);

        var request = await CaptureUpgradeRequestAsync(collector, listener);

        Assert.StartsWith("GET /_appTelemetry", request);
        Assert.DoesNotContain("authorization:", request, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dXNlcm5hbWU6cGFzc3dvcmQ=", request);
        Assert.DoesNotContain(JwtToken, request);
    }

    [Theory]
    [InlineData(AuthKind.Password, "Authorization: Basic dXNlcm5hbWU6cGFzc3dvcmQ=")]
    [InlineData(AuthKind.Jwt, "Authorization: Bearer " + JwtToken)]
    public async Task Cluster_Node_Receives_The_Cluster_Authorization_Header(AuthKind auth, string expectedHeader)
    {
        using var listener = new LoopbackListener();
        // No override is configured, so the remote counts as a node the cluster advertised.
        using var collector = CreateCollector(null, auth: auth);

        var request = await CaptureUpgradeRequestAsync(collector, listener);

        Assert.Contains(expectedHeader, request, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// TcpListener is not IDisposable on .NET Framework, so this stops it on dispose.
    /// </summary>
    private sealed class LoopbackListener : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);

        public LoopbackListener()
        {
            _listener.Start();
            Remote = new Uri($"ws://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/_appTelemetry");
        }

        public Uri Remote { get; }

        public Task<TcpClient> AcceptTcpClientAsync() => _listener.AcceptTcpClientAsync();

        public void Dispose() => _listener.Stop();
    }

    /// <summary>
    /// Runs the real connect loop against the listener and returns the raw upgrade request the listener received.
    /// The listener answers 400, so the connect fails and the loop only backs off until it is cancelled.
    /// </summary>
    private static async Task<string> CaptureUpgradeRequestAsync(AppTelemetryCollector collector, LoopbackListener listener)
    {
        using var handler = new WebSocketClientHandler(collector);
        using var cts = new CancellationTokenSource();
        handler.UpdateRemotes(new[] { listener.Remote });
        var loop = handler.StartAsync(cts.Token);

        try
        {
            var accept = listener.AcceptTcpClientAsync();
            await AwaitAsync(accept);
            using var client = accept.Result;
            var stream = client.GetStream();

            var request = new StringBuilder();
            var buffer = new byte[1024];
            while (!request.ToString().Contains("\r\n\r\n"))
            {
                var read = stream.ReadAsync(buffer, 0, buffer.Length);
                await AwaitAsync(read);
                if (read.Result == 0) break;
                request.Append(Encoding.ASCII.GetString(buffer, 0, read.Result));
            }

            var response = Encoding.ASCII.GetBytes("HTTP/1.1 400 Bad Request\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(response, 0, response.Length);
            return request.ToString();
        }
        finally
        {
            cts.Cancel();
            await AwaitAsync(loop);
        }
    }

    private static async Task AwaitAsync(Task task)
    {
        if (await Task.WhenAny(task, Task.Delay(WaitTimeout)).ConfigureAwait(false) != task)
        {
            throw new TimeoutException("The expected step did not happen.");
        }

        await task.ConfigureAwait(false);
    }

    public enum AuthKind
    {
        Password,
        Jwt
    }

    private static AppTelemetryCollector CreateCollector(Uri endpoint, ILoggerFactory loggerFactory = null, AuthKind auth = AuthKind.Password)
    {
        var options = auth == AuthKind.Jwt
            ? new ClusterOptions().WithJwtAuthentication(JwtToken)
            : new ClusterOptions().WithPasswordAuthentication("username", "password");
        options.AppTelemetry.Endpoint = endpoint;
        if (loggerFactory is not null) options.WithLogging(loggerFactory);

        var context = new ClusterContext(null, options);
        return new AppTelemetryCollector(context, new Redactor(RedactionLevel.None), NullLogger<AppTelemetryCollector>.Instance);
    }

    private sealed class CountingLoggerFactory : ILoggerFactory
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public int Count(string text) => _messages.Count(m => m.Contains(text));

        public ILogger CreateLogger(string categoryName) => new CountingLogger(_messages);

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }

        private sealed class CountingLogger : ILogger
        {
            private readonly ConcurrentQueue<string> _messages;

            public CountingLogger(ConcurrentQueue<string> messages) => _messages = messages;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
                Func<TState, Exception, string> formatter) => _messages.Enqueue(formatter(state, exception));

            public bool IsEnabled(LogLevel logLevel) => true;

            public IDisposable BeginScope<TState>(TState state) => null;
        }
    }
}
