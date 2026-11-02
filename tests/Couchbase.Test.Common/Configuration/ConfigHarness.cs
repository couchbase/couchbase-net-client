#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Management.Buckets;
using Couchbase.Test.Common.Utils;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Moq;

namespace Couchbase.Test.Common.Configuration
{
    /// <summary>
    /// Runs the SDK config handling (config handler, cluster context, buckets, real cluster nodes) with no network.
    /// Each node has one connection to <see cref="Server"/>, set up by the node like a real connection.
    /// By default the SDK services run on a <see cref="SteppableClock"/>, so time moves only in <see cref="AdvanceAsync"/>.
    /// </summary>
    internal sealed class ConfigHarness : IAsyncDisposable
    {
        private const string BarrierName = "config-harness-barrier";
        private const int QuietRounds = 3;
        private const int PoolHopsPerRound = 2;
        private static readonly TimeSpan MinQuietTime = TimeSpan.FromMilliseconds(1);
        private static readonly TimeSpan CompileGrace = TimeSpan.FromMilliseconds(50);
        private static readonly TimeSpan SettleLimit = TimeSpan.FromSeconds(10);

        private readonly NodeFactory _nodeFactory;
        private readonly IConfigHandler _handler;
        private readonly bool _polling;
        private readonly HandlerSink _sink = new();
        private readonly ConcurrentDictionary<string, CouchbaseBucket> _buckets = new();
        private int _barrierCount;
        private volatile bool _handlerStarted;

        private ConfigHarness(ClusterContext context, FakeClusterServer server, NodeFactory nodeFactory,
            IConfigHandler handler, bool polling, TimeProvider clock)
        {
            Context = context;
            Server = server;
            _nodeFactory = nodeFactory;
            _handler = handler;
            _polling = polling;
            Clock = clock;
        }

        public ClusterContext Context { get; }

        public FakeClusterServer Server { get; }

        /// <summary>
        /// The clock of the SDK services.
        /// </summary>
        public TimeProvider Clock { get; }

        public ClusterOptions Options => Context.ClusterOptions;

        /// <summary>
        /// Nodes the node factory created, including the bootstrap nodes.
        /// </summary>
        public int NodesCreated => _nodeFactory.Created;

        public ConfigVersion? GlobalVersion => Context.GlobalConfig?.ConfigVersion;

        /// <summary>
        /// Every node of the context, sorted by hostname then owner.
        /// </summary>
        public IReadOnlyList<NodeView> Nodes => Context.Nodes
            .Select(n => new NodeView(n, Options))
            .OrderBy(n => n.Hostname, StringComparer.Ordinal)
            .ThenBy(n => n.Owner, StringComparer.Ordinal)
            .ToList();

        /// <summary>
        /// Bootstraps the global map the way BootstrapGlobalAsync does, with the map in place of the CCCP fetch.
        /// Every node serves the map.
        /// </summary>
        /// <param name="pollInterval">
        /// Turns config polling on. The handler then starts in <see cref="StartPolling"/>, so the test sets the server state first.
        /// </param>
        /// <param name="clock">
        /// The clock of the SDK services. A new <see cref="SteppableClock"/> when null. Pass <see cref="TimeProvider.System"/> for real time.
        /// </param>
        public static ConfigHarness Create(ClusterMapBuilder globalMap, Action<ClusterOptions>? configure = null,
            TimeSpan? pollInterval = null, TimeProvider? clock = null)
        {
            var config = globalMap.Build();
            config.Name = BucketConfig.GlobalBucketName;

            var options = new ClusterOptions
            {
                EnableConfigPolling = pollInterval is not null,
                NetworkResolution = NetworkResolution.Default
            }.WithPasswordAuthentication("username", "password");
            if (pollInterval is not null) options.ConfigPollInterval = pollInterval.Value;
            options.AppTelemetry.Enabled = false;
            options.LoggingMeterOptions.Enabled(false);
            configure?.Invoke(options);
            // Polling reads the connection string after a pass where no node returned a config.
            options.ConnectionString ??= $"couchbase://{config.GetNodes()[0].Hostname}";
            clock ??= new SteppableClock();
            options.AddClusterService<TimeProvider, TimeProvider>(clock);

            var server = new FakeClusterServer();
            server.SetMap(globalMap);
            var nodeFactory = new NodeFactory(server);
            options.AddClusterService<IClusterNodeFactory, NodeFactory>(nodeFactory);
            // The real bootstrapper polls on a timer and rebootstraps over the network when a bucket has errors.
            options.AddClusterService<IBootstrapperFactory, IdleBootstrapper>(new IdleBootstrapper());

            // A scope reads the cluster for its search index manager.
            var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(), options);
            nodeFactory.Attach(context);

            config.SetEffectiveNetworkResolution(options);
            context.GlobalConfig = config;
            foreach (var adapter in config.GetNodes())
            {
                context.AddNode(nodeFactory.Create(HostEndpointWithPort.Create(adapter, options), adapter));
            }

            var handler = context.ServiceProvider.GetRequiredService<IConfigHandler>();
            var polling = pollInterval is not null;
            var harness = new ConfigHarness(context, server, nodeFactory, handler, polling, clock);
            handler.Subscribe(context);
            handler.Subscribe(harness._sink);
            if (!polling)
            {
                handler.Start(withPolling: false);
                harness._handlerStarted = true;
            }
            return harness;
        }

        /// <summary>
        /// Starts the config handler with polling. Each poll pass fetches the map from every KV node in turn,
        /// except bucket nodes, which rely on brief push.
        /// </summary>
        public void StartPolling()
        {
            if (!_polling) throw new InvalidOperationException("Create the harness with a poll interval to poll.");
            _handler.Start(withPolling: true);
            _handlerStarted = true;
        }

        /// <summary>
        /// Opens a bucket the way CreateAndBootStrapBucketAsync does. Every node serves the map,
        /// and the bucket fetches it over CCCP from the first bootstrap node.
        /// </summary>
        public async Task<CouchbaseBucket> OpenBucketAsync(ClusterMapBuilder bucketMap)
        {
            Server.SetMap(bucketMap);
            var endpoint = Context.GlobalConfig.GetBootstrapEndpoints(Options.EnableTls).First();
            var node = Context.GetUnassignedNode(endpoint);
            var newNode = node is null;
            node ??= await _nodeFactory.CreateAndConnectAsync(endpoint).ConfigureAwait(false);

            await node.SelectBucketAsync(bucketMap.Name).ConfigureAwait(false);
            var config = await node.GetClusterMap().ConfigureAwait(false);

            var bucket = (CouchbaseBucket)Context.ServiceProvider.GetRequiredService<IBucketFactory>()
                .Create(bucketMap.Name, BucketType.Couchbase, config);
            node.Owner = bucket;
            node.NodesAdapter = config.GetNodes().Find(x => HostEndpointWithPort.Create(x, Options) == endpoint)
                                ?? throw new InvalidOperationException($"The bucket map has no node for {endpoint}.");
            if (newNode)
            {
                Context.AddNode(node);
            }

            await bucket.BootstrapAsync(node).ConfigureAwait(false);
            var bootstrappable = (IBootstrappable)bucket;
            if (!bootstrappable.IsBootstrapped)
            {
                await bucket.DisposeAsync().ConfigureAwait(false);
                throw new AggregateException($"Bucket {bucketMap.Name} did not bootstrap.", bootstrappable.DeferredExceptions);
            }

            Context.RegisterBucket(bucket);
            _buckets[bucket.Name] = bucket;
            return bucket;
        }

        public void Publish(ClusterMapBuilder map) => Publish(map.Build());

        public void Publish(BucketConfig config) => Context.PublishConfig(config);

        /// <summary>
        /// Waits until every config published before this call is processed by every subscriber.
        /// </summary>
        /// <remarks>
        /// The handler processes one config at a time and awaits all subscribers, so a barrier config
        /// that only the harness accepts completes after every earlier config.
        /// </remarks>
        public Task DrainAsync()
        {
            var name = $"{BarrierName}-{Interlocked.Increment(ref _barrierCount)}";
            var processed = _sink.Expect(name, default, times: 1);
            Context.PublishConfig(new BucketConfig { Name = name });
            return processed;
        }

        /// <summary>
        /// Waits until the handler has dispatched the config at least <paramref name="times"/> times since the harness
        /// was created or <see cref="ForgetDispatched"/> was last called. Call <see cref="DrainAsync"/> next to wait until
        /// every subscriber has processed it.
        /// </summary>
        /// <param name="timeout">Only guards against a hang.</param>
        public async Task WaitForPublishedAsync(string name, ConfigVersion version, TimeSpan timeout, int times = 1)
        {
            var dispatched = _sink.Expect(name, version, times);
            using var cts = new CancellationTokenSource();
            var completed = await Task.WhenAny(dispatched, Task.Delay(timeout, cts.Token)).ConfigureAwait(false);
            cts.Cancel();
            if (completed != dispatched)
            {
                throw new TimeoutException(
                    $"The handler did not dispatch {name} {version} {times} time(s) in {timeout}. Dispatched: {_sink.Dispatched()}.");
            }
        }

        /// <summary>
        /// Returns once the pipeline has no work left that it can do without the clock moving.
        /// </summary>
        /// <remarks>
        /// Each round lets queued thread pool work run, drains the handler, and reads four signals. They are the configs
        /// dispatched (barriers left out), the requests the server answered, the timers created and the pending timers.
        /// The server answers a request before the send returns, so a fetch counts as soon as it is sent.
        /// The pipeline is idle once the signals stay the same for <see cref="QuietRounds"/> rounds and <see cref="MinQuietTime"/>
        /// of real time, so each call takes at least that long.
        /// Code that runs between two signals shows nothing, for example a push handler that woke up and has not sent its fetch yet.
        /// The first run of such code in the process can take milliseconds, mostly to compile it. So a round in which the runtime
        /// compiled methods restarts the quiet count, for up to <see cref="CompileGrace"/> after the last signal change.
        /// Work that shows nothing for longer is missed.
        /// It throws a <see cref="TimeoutException"/> after <see cref="SettleLimit"/> of real time, so a pipeline that does not
        /// go quiet, or a handler that waits on the clock, fails the test instead of hanging it.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The harness does not run on a <see cref="SteppableClock"/>.</exception>
        public async Task SettleAsync()
        {
            var clock = SteppableClockFor(nameof(SettleAsync));
            var elapsed = Stopwatch.StartNew();
            using var limit = new CancellationTokenSource(SettleLimit);
            var limitReached = Task.Delay(Timeout.InfiniteTimeSpan, limit.Token);
            var last = ReadSignals(clock);
            var lastCompiled = CompiledMethodCount();
            var lastSignalChange = TimeSpan.Zero;
            var quietSince = TimeSpan.Zero;
            var quietRounds = 0;
            while (quietRounds < QuietRounds || elapsed.Elapsed - quietSince < MinQuietTime)
            {
                await YieldToThreadPoolAsync().ConfigureAwait(false);

                // Before StartPolling the handler takes no configs, so there is nothing to drain.
                if (_handlerStarted)
                {
                    var drained = DrainAsync();
                    if (await Task.WhenAny(drained, limitReached).ConfigureAwait(false) != drained)
                    {
                        throw NotSettled("The config handler did not drain", clock);
                    }
                    await drained.ConfigureAwait(false);
                }

                var now = elapsed.Elapsed;
                var signals = ReadSignals(clock);
                var compiled = CompiledMethodCount();
                if (signals != last)
                {
                    last = signals;
                    lastSignalChange = now;
                    quietSince = now;
                    quietRounds = 0;
                }
                else if (compiled != lastCompiled && now - lastSignalChange < CompileGrace)
                {
                    quietSince = now;
                    quietRounds = 0;
                }
                else
                {
                    quietRounds++;
                }
                lastCompiled = compiled;

                if (elapsed.Elapsed > SettleLimit)
                {
                    throw NotSettled("The config pipeline did not go quiet", clock);
                }
            }
        }

        /// <summary>
        /// Moves the clock forward by <paramref name="duration"/>, one due timer at a time, and settles after each step.
        /// </summary>
        /// <remarks>
        /// One big step would squash the timeline. The clock fires every timer due in one call, and a timer that the
        /// woken code creates is due relative to the time at which it is created.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The harness does not run on a <see cref="SteppableClock"/>.</exception>
        public async Task AdvanceAsync(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration), duration, "Time cannot go back.");

            var clock = SteppableClockFor(nameof(AdvanceAsync));
            var end = clock.GetUtcNow() + duration;
            await SettleAsync().ConfigureAwait(false);
            while (clock.NextDueTime is { } due && due <= end)
            {
                clock.Advance(NotNegative(due - clock.GetUtcNow()));
                await SettleAsync().ConfigureAwait(false);
            }

            clock.Advance(NotNegative(end - clock.GetUtcNow()));
            await SettleAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Forgets the configs dispatched so far. A benchmark calls it between runs, because each wait scans the record.
        /// </summary>
        public void ForgetDispatched() => _sink.Forget();

        /// <summary>
        /// Calls the context subscriber directly, outside the handler queue.
        /// </summary>
        public Task ApplyGlobalAsync(BucketConfig config)
        {
            // The handler and the CCCP fetch both set this before a subscriber sees the config.
            config.NetworkResolution = Options.EffectiveNetworkResolution;
            return Context.ConfigUpdatedAsync(config);
        }

        /// <summary>
        /// Calls the bucket subscriber directly, outside the handler queue, like a DNS SRV rebootstrap does.
        /// </summary>
        public Task ApplyBucketAsync(string bucketName, BucketConfig config)
        {
            config.NetworkResolution = Options.EffectiveNetworkResolution;
            return _buckets[bucketName].ConfigUpdatedAsync(config);
        }

        public BucketView Bucket(string name) => new(_buckets[name]);

        /// <summary>
        /// Makes node creation throw for this host until <see cref="AllowNodeCreation"/> is called.
        /// </summary>
        public void FailNodeCreation(string host) => _nodeFactory.SetFailing(host, failing: true);

        public void AllowNodeCreation(string host) => _nodeFactory.SetFailing(host, failing: false);

        private SteppableClock SteppableClockFor(string caller) =>
            Clock as SteppableClock ?? throw new InvalidOperationException(
                $"{caller} needs a harness on a {nameof(SteppableClock)}, but this one runs on {Clock.GetType().Name}.");

        private (long Dispatched, long Requests, long TimersCreated, int PendingTimers) ReadSignals(SteppableClock clock) =>
            (_sink.DispatchedCount, Server.RequestCount, clock.TimersCreated, clock.PendingTimers.Count);

        private TimeoutException NotSettled(string reason, SteppableClock clock)
        {
            var now = clock.GetUtcNow();
            var pending = string.Join(", ", clock.PendingTimers.Select(t => $"due in {(t.DueTime - now).TotalMilliseconds} ms"));
            var (dispatched, requests, timersCreated, _) = ReadSignals(clock);
            return new TimeoutException(
                $"{reason} in {SettleLimit.TotalSeconds} s of real time. Configs dispatched {dispatched} [{_sink.Dispatched()}], " +
                $"requests answered {requests}, timers created {timersCreated}, pending timers [{pending}].");
        }

        // The count covers the whole process, so other tests that compile code delay the quiet rounds too.
        // .NET Framework has no such count.
        private static long CompiledMethodCount() =>
#if NET6_0_OR_GREATER
            System.Runtime.JitInfo.GetCompiledMethodCount();
#else
            0;
#endif

        // A Task.Yield on a pool thread queues to the global queue, behind the work queued there before it.
        // The thread also runs its own local queue first, where Task.Run and most continuations go.
        private static Task YieldToThreadPoolAsync() => Task.Run(static async () =>
        {
            for (var i = 0; i < PoolHopsPerRound; i++)
            {
                await Task.Yield();
            }
        });

        private static TimeSpan NotNegative(TimeSpan duration) => duration < TimeSpan.Zero ? TimeSpan.Zero : duration;

        public async ValueTask DisposeAsync()
        {
            foreach (var bucket in _buckets.Values)
            {
                await bucket.DisposeAsync().ConfigureAwait(false);
            }

            // Also disposes the config handler and every node.
            Context.Dispose();
        }

        internal sealed class NodeView
        {
            internal NodeView(IClusterNode node, ClusterOptions options)
            {
                var adapter = node.NodesAdapter;
                Hostname = node.EndPoint.Host;
                NodeUuid = node.NodeUuid;
                Owner = node.Owner?.Name;
                Version = adapter?.ConfigVersion;
                // Built from the adapter because reading ClusterNode.QueryUri stamps LastQueryActivity.
                QueryUri = adapter is { IsQueryNode: true } ? adapter.GetQueryUri(options) : null;
            }

            public string Hostname { get; }

            public string NodeUuid { get; }

            /// <summary>
            /// The bucket that owns the node, or null for a global node.
            /// </summary>
            public string? Owner { get; }

            /// <summary>
            /// The version of the config the node adapter came from.
            /// </summary>
            public ConfigVersion? Version { get; }

            public Uri? QueryUri { get; }

            public override string ToString() => $"{Hostname} owner={Owner ?? "none"} rev={Version} query={QueryUri}";
        }

        internal sealed class BucketView
        {
            internal BucketView(CouchbaseBucket bucket)
            {
                var config = bucket.CurrentConfig;
                Version = config?.ConfigVersion;
                KeyMapperRev = bucket.KeyMapper?.Rev;
                Nodes = bucket.Nodes.Select(n => n.EndPoint.Host).OrderBy(h => h, StringComparer.Ordinal).ToList();

                var servers = config?.VBucketServerMap.ServerList ?? [];
                ServerGroups = config?.ServerGroupNodeIndexes.ToDictionary(
                                   g => g.Key,
                                   g => g.Value.Select(i => servers[i]).OrderBy(s => s, StringComparer.Ordinal).ToArray())
                               ?? new Dictionary<string, string[]>();
            }

            /// <summary>
            /// The version of <see cref="BucketBase.CurrentConfig"/>.
            /// </summary>
            public ConfigVersion? Version { get; }

            public ulong? KeyMapperRev { get; }

            /// <summary>
            /// Each server group of the current config, with its vbucket server list entries (host:port).
            /// </summary>
            public IReadOnlyDictionary<string, string[]> ServerGroups { get; }

            /// <summary>
            /// Hostnames of the nodes the bucket sends operations to.
            /// </summary>
            public IReadOnlyList<string> Nodes { get; }
        }

        /// <summary>
        /// Wraps the real node factory, so nodes are real <see cref="ClusterNode"/> objects with an offline pool.
        /// </summary>
        private sealed class NodeFactory(FakeClusterServer server) : IClusterNodeFactory
        {
            private readonly ConcurrentDictionary<string, bool> _failingHosts = new(StringComparer.Ordinal);
            private ClusterNodeFactory? _inner;
            private int _created;

            public int Created => Volatile.Read(ref _created);

            public void Attach(ClusterContext context)
            {
                var services = context.ServiceProvider;
                _inner = new ClusterNodeFactory(context, new ServerPoolFactory(server),
                    services.GetRequiredService<ILogger<ClusterNode>>(),
                    services.GetRequiredService<ObjectPool<OperationBuilder>>(),
                    new NoSaslMechanismFactory(),
                    services.GetRequiredService<Redactor>(),
                    services.GetRequiredService<IRequestTracer>(),
                    services.GetRequiredService<IOperationConfigurator>());
            }

            public void SetFailing(string host, bool failing)
            {
                if (failing) _failingHosts[host] = true;
                else _failingHosts.TryRemove(host, out _);
            }

            // The real factory completes at once because the fake server answers before each send returns.
            public IClusterNode Create(HostEndpointWithPort endPoint, NodeAdapter? nodeAdapter) =>
                CreateAndConnectAsync(endPoint, nodeAdapter).GetAwaiter().GetResult();

            public Task<IClusterNode> CreateAndConnectAsync(HostEndpointWithPort endPoint, CancellationToken cancellationToken = default) =>
                CreateAndConnectAsync(endPoint, null, cancellationToken);

            public async Task<IClusterNode> CreateAndConnectAsync(HostEndpointWithPort endPoint, NodeAdapter? nodeAdapter,
                CancellationToken cancellationToken = default)
            {
                if (_failingHosts.ContainsKey(endPoint.Host))
                {
                    throw new InvalidOperationException($"The harness fails node creation for {endPoint.Host}.");
                }

                var node = await _inner!.CreateAndConnectAsync(endPoint, nodeAdapter, cancellationToken).ConfigureAwait(false);
                Interlocked.Increment(ref _created);
                return node;
            }
        }

        private sealed class ServerPoolFactory(FakeClusterServer server) : IConnectionPoolFactory
        {
            public IConnectionPool Create(ClusterNode clusterNode) => new ServerPool(clusterNode, server);
        }

        /// <summary>
        /// A pool with one connection to the fake server. Like a real pool, it lets the node set up the connection.
        /// </summary>
        private sealed class ServerPool(ClusterNode node, FakeClusterServer server) : IConnectionPool, IAsyncDisposable
        {
            private FakeClusterServer.Connection? _connection;

            public HostEndpointWithPort EndPoint => node.EndPoint;
            public int Size => _connection is null ? 0 : 1;
            public int MinimumSize => 1;
            public int MaximumSize => 1;
            public int PendingSends => 0;

            public async Task InitializeAsync(CancellationToken cancellationToken = default)
            {
                var connection = server.Connect(node.EndPoint);
                try
                {
                    await ((IConnectionInitializer)node).InitializeConnectionAsync(connection, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch
                {
                    connection.Dispose();
                    throw;
                }
                _connection = connection;
            }

            public Task SendAsync(IOperation op, CancellationToken cancellationToken = default)
            {
                if (_connection is null)
                {
                    // A node with no data service has no connection, like a real pool.
                    op.HandleOperationCompleted(AsyncStateBase.BuildErrorResponse(op.Opaque, ResponseStatus.TransportFailure));
                    return Task.CompletedTask;
                }
                return op.SendAsync(_connection, cancellationToken);
            }

            public async Task<bool> TrySendImmediatelyAsync(IOperation op, CancellationToken cancellationToken = default)
            {
                await SendAsync(op, cancellationToken).ConfigureAwait(false);
                return true;
            }

            public ValueTask<IAsyncDisposable> FreezePoolAsync(CancellationToken cancellationToken = default) => new(this);

            public Task SelectBucketAsync(string name, CancellationToken cancellationToken = default) =>
                _connection is null
                    ? Task.CompletedTask
                    : ((IConnectionInitializer)node).SelectBucketAsync(_connection, name, cancellationToken);

            public IEnumerable<IConnection> GetConnections() => _connection is null ? [] : [_connection];

            public Task ScaleAsync(int delta) => Task.CompletedTask;

            public ValueTask DisposeAsync() => default;

            public void Dispose() => _connection?.Dispose();
        }

        // The fake server accepts every client, so the nodes skip SASL.
        private sealed class NoSaslMechanismFactory : ISaslMechanismFactory, ISaslMechanism
        {
            public MechanismType MechanismType => MechanismType.ScramSha512;

            public Task AuthenticateAsync(IConnection connection, CancellationToken cancellationToken = default)
            {
                connection.IsAuthenticated = true;
                return Task.CompletedTask;
            }

            public ISaslMechanism CreatePasswordMechanism(MechanismType mechanismType, string username, string password) => this;

            public ISaslMechanism CreatePasswordMechanism(IConnection connection, string username, string password) => this;

            public ISaslMechanism CreateOAuthBearerMechanism(string token) => this;
        }

        private sealed class IdleBootstrapper : IBootstrapperFactory, IBootstrapper
        {
            public TimeSpan SleepDuration { get; set; }

            public IBootstrapper Create(TimeSpan sleepDuration) => this;

            public void Start(IBootstrappable subject)
            {
            }

            public void Dispose()
            {
            }
        }

        /// <summary>
        /// Records every config the handler dispatches, so a test can wait for one. Barrier configs are not recorded.
        /// </summary>
        private sealed class HandlerSink : IConfigUpdateEventSink
        {
            private readonly object _lock = new();
            private readonly List<(string? Name, ConfigVersion Version)> _dispatched = new();
            private readonly List<Waiter> _waiters = new();
            private long _dispatchedCount;

            public string Name => BarrierName;

            /// <summary>
            /// How many configs the handler dispatched. <see cref="Forget"/> does not reset it.
            /// </summary>
            public long DispatchedCount => Interlocked.Read(ref _dispatchedCount);

            public IEnumerable<IClusterNode> ClusterNodes => [];

            public Task Expect(string name, ConfigVersion version, int times)
            {
                lock (_lock)
                {
                    var remaining = times - _dispatched.Count(d => d.Name == name && d.Version.Equals(version));
                    if (remaining <= 0) return Task.CompletedTask;

                    var waiter = new Waiter(name, version, remaining);
                    _waiters.Add(waiter);
                    return waiter.Done.Task;
                }
            }

            public void Forget()
            {
                lock (_lock) _dispatched.Clear();
            }

            public string Dispatched()
            {
                lock (_lock)
                {
                    return string.Join(", ", _dispatched.Select(d => $"{d.Name} {d.Version}"));
                }
            }

            public Task ConfigUpdatedAsync(BucketConfig newConfig)
            {
                var barrier = newConfig.Name is not null && newConfig.Name.StartsWith(BarrierName, StringComparison.Ordinal);
                lock (_lock)
                {
                    if (!barrier)
                    {
                        _dispatched.Add((newConfig.Name, newConfig.ConfigVersion));
                        Interlocked.Increment(ref _dispatchedCount);
                    }

                    foreach (var waiter in _waiters.Where(w => w.Name == newConfig.Name && w.Version.Equals(newConfig.ConfigVersion)).ToList())
                    {
                        if (--waiter.Remaining > 0) continue;

                        _waiters.Remove(waiter);
                        waiter.Done.TrySetResult(true);
                    }
                }
                return Task.CompletedTask;
            }

            private sealed class Waiter(string name, ConfigVersion version, int remaining)
            {
                public string Name { get; } = name;

                public ConfigVersion Version { get; } = version;

                public int Remaining { get; set; } = remaining;

                public TaskCompletionSource<bool> Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }
}

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2026 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
