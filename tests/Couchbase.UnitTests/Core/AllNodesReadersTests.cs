using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Authentication.Authenticators;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.Core.Sharding;
using Couchbase.Diagnostics;
using Couchbase.KeyValue;
using Couchbase.UnitTests.Helpers;
using Couchbase.UnitTests.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

#nullable enable

namespace Couchbase.UnitTests.Core;

/// <summary>
/// What an app sees from the readers that cover every KV connection of the client, for one open bucket on three KV
/// hosts and one Query only host.
/// </summary>
public class AllNodesReadersTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task DiagnosticsReport_KvEntries_HoldOneBucketEntryPerKvHost()
    {
        using var client = OneBucketClient.Create();

        var report = await DiagnosticsReportProvider.CreateDiagnosticsReportAsync(client.Context, "report")
            .WaitAsync(Timeout);

        AssertBucketKvEntries(client, report.Services["kv"]);
    }

    [Fact]
    public async Task DiagnosticsReport_ViewEntries_HoldOneBucketEntryPerKvHost()
    {
        using var client = OneBucketClient.Create();

        var report = await DiagnosticsReportProvider.CreateDiagnosticsReportAsync(client.Context, "report")
            .WaitAsync(Timeout);

        var views = report.Services["view"].ToList();
        Assert.All(views, e => Assert.Equal(OneBucketClient.BucketName, e.Scope));
        Assert.Equal(OneBucketClient.KvHosts.Select(h => $"{h}:11210").OrderBy(r => r, StringComparer.Ordinal).ToList(),
            views.Select(e => e.Remote ?? "").OrderBy(r => r, StringComparer.Ordinal).ToList());
    }

    [Fact]
    public async Task ClusterPing_KvEntries_HoldOneBucketEntryPerKvHost()
    {
        using var client = OneBucketClient.Create();

        var report = await DiagnosticsReportProvider.CreatePingReportAsync(client.Context, client.Context.GlobalConfig,
            new PingOptions().ServiceTypes(ServiceType.KeyValue)).WaitAsync(Timeout);

        var kv = report.Services["kv"].ToList();
        AssertBucketKvEntries(client, kv);
        Assert.All(kv, e => Assert.Equal(ServiceState.Ok, e.State));
    }

    [Fact]
    public async Task BucketPing_KvEntries_HoldOnlyTheBucketEntries()
    {
        using var client = OneBucketClient.Create();

        var report = await client.Bucket.PingAsync(new PingOptions().ServiceTypes(ServiceType.KeyValue))
            .WaitAsync(Timeout);

        var kv = report.Services["kv"].ToList();
        Assert.All(kv, e => Assert.Equal(OneBucketClient.BucketName, e.Scope));
        Assert.Equal(client.BucketRemotes, kv.Select(e => e.Remote).OrderBy(r => r, StringComparer.Ordinal));
        Assert.All(kv, e => Assert.Equal(ServiceState.Ok, e.State));
    }

    [Fact]
    public async Task ClusterWaitUntilReady_WaitsForKvAndQuery_AndIsOnline()
    {
        using var client = OneBucketClient.Create();
        var options = new WaitUntilReadyOptions();

        var expected = WaitUntilReadyEvaluator.ExpectedServices(client.Context, client.Context.GlobalConfig,
            options.EffectiveServiceTypes(client.Context), bucketLevel: false);
        var state = await PollClusterAsync(client, options);

        Assert.Equal(new HashSet<ServiceType> { ServiceType.KeyValue, ServiceType.Query }, expected);
        Assert.Equal(ClusterState.Online, state);
    }

    [Fact]
    public async Task ClusterWaitUntilReady_BucketConnectionFailsItsPing_IsDegraded()
    {
        using var client = OneBucketClient.Create();
        client.FailPing(client.BucketNodes[OneBucketClient.KvHosts[1]]);

        var state = await PollClusterAsync(client, new WaitUntilReadyOptions().DesiredState(ClusterState.Degraded));

        Assert.Equal(ClusterState.Degraded, state);
    }

    [Fact]
    public async Task JwtAuthenticator_ReauthenticatesEveryKvNodeOnce()
    {
        var options = new ClusterOptions()
            .WithJwtAuthentication("initial-token")
            .WithConnectionString("couchbases://localhost");
        OneBucketClient.Configure(options);
        var mockCluster = new Mock<Cluster>(options) { CallBase = true };
        mockCluster.Setup(c => c.EnsureBootstrapped()).Returns(Task.CompletedTask);
        using var cluster = mockCluster.Object;
        var context = (ClusterContext)typeof(Cluster)
            .GetField("_context", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(cluster)!;
        using var client = new OneBucketClient(context, hostWithoutFeatures: null);

        var reached = new HashSet<IClusterNode>();
        var everyNode = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        foreach (var node in client.KvNodes)
        {
            node.Setup(n => n.ReauthenticateKvConnectionsAsync(It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    lock (reached)
                    {
                        reached.Add(node.Object);
                        if (reached.Count == client.KvNodes.Count) everyNode.TrySetResult(true);
                    }
                    return Task.CompletedTask;
                });
        }

        cluster.Authenticator(new JwtAuthenticator("new-token"));
        await everyNode.Task.WaitAsync(Timeout);

        Assert.All(client.KvNodes,
            node => node.Verify(n => n.ReauthenticateKvConnectionsAsync(It.IsAny<CancellationToken>()), Times.Once));
        client.QueryNode.Verify(n => n.ReauthenticateKvConnectionsAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void FeatureSupport_EveryKvHostHasTheFeatures_IsSupported()
    {
        using var client = OneBucketClient.Create();

        Assert.True(client.Context.SupportsPreserveTtl);
        Assert.True(client.Context.SupportsBinaryXattr);
    }

    [Fact]
    public void FeatureSupport_OneKvHostLacksTheFeatures_IsNotSupported()
    {
        using var client = OneBucketClient.Create(hostWithoutFeatures: OneBucketClient.KvHosts[1]);

        Assert.False(client.Context.SupportsPreserveTtl);
        Assert.False(client.Context.SupportsBinaryXattr);
    }

    /// <remarks>
    /// The entries with no scope are the connections that no bucket owns. Their count is not pinned here.
    /// </remarks>
    private static void AssertBucketKvEntries(OneBucketClient client, IEnumerable<IEndpointDiagnostics> entries)
    {
        var kv = entries.ToList();
        Assert.All(kv, e => Assert.True(e.Scope is null or OneBucketClient.BucketName, $"Unexpected scope {e.Scope}."));
        Assert.Equal(client.BucketRemotes, kv.Where(e => e.Scope == OneBucketClient.BucketName)
            .Select(e => e.Remote).OrderBy(r => r, StringComparer.Ordinal));
    }

    private static async Task<ClusterState?> PollClusterAsync(OneBucketClient client, WaitUntilReadyOptions options)
    {
        using var cts = new CancellationTokenSource(Timeout);
        ClusterState? state = null;
        await WaitUntilReadyEvaluator.PollAsync(client.Context, options, bucketLevel: false,
            () => client.Context.GlobalConfig,
            _ => new ValueTask<bool>(true),
            s => state = s,
            NullLogger.Instance, cts.Token).WaitAsync(Timeout);
        return state;
    }

    /// <summary>
    /// The nodes of a client with one open bucket on three KV hosts and one Query only host. Each KV host has one
    /// connection per node.
    /// </summary>
    private sealed class OneBucketClient : IDisposable
    {
        internal const string BucketName = "default";

        internal static readonly string[] KvHosts = ["node-0", "node-1", "node-2"];

        private const string QueryHost = "node-3";

        private static readonly ServerFeatures[] AllFeatures = [ServerFeatures.PreserveTtl, ServerFeatures.SubdocBinaryXattr];

        private ulong _lastConnectionId;

        internal OneBucketClient(ClusterContext context, string? hostWithoutFeatures)
        {
            Context = context;
            context.GlobalConfig = Config(BucketConfig.GlobalBucketName, withQueryHost: true);

            Bucket = new CouchbaseBucket(BucketName,
                context,
                new Mock<IScopeFactory>().Object,
                new Mock<IRetryOrchestrator>().Object,
                new Mock<IVBucketKeyMapperFactory>().Object,
                new Mock<ILogger<CouchbaseBucket>>().Object,
                new Redactor(RedactionLevel.None),
                new Mock<IBootstrapperFactory>().Object,
                NoopRequestTracer.Instance,
                new Mock<IOperationConfigurator>().Object,
                new BestEffortRetryStrategy(),
                Config(BucketName, withQueryHost: false),
                context.ServiceProvider.GetRequiredService<IConfigRefresherFactory>());

            for (var i = 0; i < KvHosts.Length; i++)
            {
                AddKvHost(KvHosts[i], i, KvHosts[i] == hostWithoutFeatures ? [] : AllFeatures);
            }

            QueryNode = CreateNode(QueryHost, hasKv: false, connectionAddress: null, AllFeatures);
            context.AddNode(QueryNode.Object);

            context.RegisterBucket(Bucket);
            BucketRemotes = BucketNodes.Values
                .SelectMany(n => n.Object.ConnectionPool.GetConnections())
                .Select(c => c.EndPoint.ToString()!)
                .OrderBy(r => r, StringComparer.Ordinal)
                .ToList();
        }

        public ClusterContext Context { get; }

        public CouchbaseBucket Bucket { get; }

        /// <summary>
        /// Every node with KV, of the cluster and of the bucket.
        /// </summary>
        public List<Mock<IClusterNode>> KvNodes { get; } = new();

        /// <summary>
        /// The node of the bucket on each KV host.
        /// </summary>
        public Dictionary<string, Mock<IClusterNode>> BucketNodes { get; } = new();

        public Mock<IClusterNode> QueryNode { get; }

        /// <summary>
        /// The remote of each connection of the bucket, sorted.
        /// </summary>
        public List<string> BucketRemotes { get; }

        public static OneBucketClient Create(string? hostWithoutFeatures = null)
        {
            var options = new ClusterOptions().WithPasswordAuthentication("username", "password");
            Configure(options);
            return new OneBucketClient(new ClusterContext(new CancellationTokenSource(), options), hostWithoutFeatures);
        }

        /// <summary>
        /// Keeps the context from the network. Configs go nowhere and every HTTP ping succeeds.
        /// </summary>
        public static void Configure(ClusterOptions options)
        {
            options.AddClusterService(new Mock<IConfigHandler>().Object);
            options.AddClusterService<ICouchbaseHttpClientFactory, MockHttpClientFactory>(new MockHttpClientFactory(() =>
                new HttpClient(FakeHttpMessageHandler.Create(_ => new HttpResponseMessage(HttpStatusCode.OK)))));
        }

        public void FailPing(Mock<IClusterNode> node) =>
            node.Setup(n => n.ExecuteOp(It.IsAny<IConnection>(), It.IsAny<IOperation>(), It.IsAny<CancellationTokenPair>()))
                .ThrowsAsync(new TimeoutException());

        public void Dispose() => Context.Dispose();

        // The bucket owns the only node of the host, which sits in the context and in the bucket.
        private void AddKvHost(string host, int index, ServerFeatures[] features)
        {
            var node = CreateNode(host, hasKv: true, new IPEndPoint(IPAddress.Parse($"10.0.0.{index + 1}"), 11210),
                features);
            node.Object.Owner = Bucket;
            Context.AddNode(node.Object);
            Bucket.Nodes.Add(node.Object);
            KvNodes.Add(node);
            BucketNodes[host] = node;
        }

        private Mock<IClusterNode> CreateNode(string host, bool hasKv, IPEndPoint? connectionAddress,
            ServerFeatures[] features)
        {
            var node = new Mock<IClusterNode>();
            node.SetupGet(x => x.EndPoint).Returns(new HostEndpointWithPort(host, 11210));
            node.SetupGet(x => x.KeyEndPoints).Returns(new List<HostEndpointWithPort> { new(host, 11210) });
            node.SetupGet(x => x.HasKv).Returns(hasKv);
            node.SetupGet(x => x.HasViews).Returns(hasKv);
            node.SetupGet(x => x.HasQuery).Returns(true);
            node.SetupGet(x => x.QueryUri).Returns(new Uri($"http://{host}:8093/query/service"));
            node.SetupGet(x => x.ServerFeatures).Returns(new ServerFeatureSet(features));
            node.SetupProperty(x => x.Owner);
            node.Setup(x => x.ExecuteOp(It.IsAny<IConnection>(), It.IsAny<IOperation>(), It.IsAny<CancellationTokenPair>()))
                .ReturnsAsync(ResponseStatus.Success);

            var connections = new List<IConnection>();
            if (connectionAddress is not null)
            {
                var connection = new Mock<IConnection>();
                connection.SetupGet(x => x.ConnectionId).Returns(++_lastConnectionId);
                connection.SetupGet(x => x.EndPoint).Returns(connectionAddress);
                connection.SetupGet(x => x.LocalEndPoint).Returns(new IPEndPoint(IPAddress.Loopback, 50000));
                connections.Add(connection.Object);
            }

            var pool = new Mock<IConnectionPool>();
            pool.Setup(x => x.GetConnections()).Returns(connections);
            node.SetupGet(x => x.ConnectionPool).Returns(pool.Object);
            return node;
        }

        private static BucketConfig Config(string name, bool withQueryHost)
        {
            var nodesExt = KvHosts
                .Select(host => new NodesExt
                {
                    Hostname = host,
                    Services = new Services { Kv = 11210, Capi = 8092, N1Ql = 8093 }
                })
                .ToList();
            if (withQueryHost)
            {
                nodesExt.Add(new NodesExt { Hostname = QueryHost, Services = new Services { N1Ql = 8093 } });
            }

            return new BucketConfig { Name = name, NodesExt = nodesExt };
        }
    }
}
