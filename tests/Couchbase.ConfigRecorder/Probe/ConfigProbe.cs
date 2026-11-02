using System.Diagnostics;
using System.Text.Json.Nodes;
using Couchbase.ConfigRecorder.Tap;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;

namespace Couchbase.ConfigRecorder.Probe;

/// <summary>
/// Asks every KV node for its cluster config and its bucket config on its own connections, apart from
/// the recorded SDK instance. It runs a second ClusterContext that is never bootstrapped and only uses
/// it to build ClusterNode objects, so TLS, HELLO, auth, select bucket and the fetch are SDK code.
/// A fetch runs every interval, and at once when a probe connection receives a clustermap push.
/// </summary>
internal sealed class ConfigProbe : IAsyncDisposable
{
    private const string Stream = "probe";

    private readonly Recorder _recorder;
    private readonly TapHub _tap;
    private readonly ClusterOptions _options;
    private readonly string _bucket;
    private readonly TimeSpan _interval;
    private readonly IReadOnlyList<HostEndpointWithPort> _seeds;
    private readonly ClusterContext _context;
    private readonly IClusterNodeFactory _nodeFactory;
    private readonly CancellationTokenSource _cts = new();
    private readonly CancellationTokenSource _contextCts = new();
    private readonly Dictionary<HostEndpointWithPort, NodeProbe> _nodes = new();
    private readonly object _lock = new();
    private BucketConfig? _latestCluster;
    private BucketConfig? _latestBucket;
    private Task? _reconciler;

    public ConfigProbe(Recorder recorder, TapHub tap, ClusterOptions probeOptions, string bucket, TimeSpan interval,
        IEnumerable<HostEndpointWithPort> seeds)
    {
        _recorder = recorder;
        _tap = tap;
        _options = probeOptions;
        _bucket = bucket;
        _interval = interval;
        _seeds = seeds.Distinct().ToList();
        _context = new ClusterContext(null, _contextCts, probeOptions);
        _nodeFactory = (IClusterNodeFactory)(_context.ServiceProvider.GetService(typeof(IClusterNodeFactory))
            ?? throw new InvalidOperationException("No cluster node factory."));
    }

    public (int Up, int Total) Health()
    {
        lock (_lock)
        {
            var scopes = _nodes.Values.SelectMany(n => n.Scopes).ToList();
            return (scopes.Count(s => s.IsUp), scopes.Count);
        }
    }

    public void Start()
    {
        _recorder.Write(Stream, "start", new JsonObject
        {
            ["seeds"] = new JsonArray(_seeds.Select(s => (JsonNode?)JsonValue.Create(s.ToString())).ToArray()),
            ["networkResolution"] = _options.EffectiveNetworkResolution,
            ["tls"] = _options.EffectiveEnableTls,
            ["intervalMs"] = _interval.TotalMilliseconds,
        });
        _tap.ProbePushed += OnPush;
        _reconciler = Task.Run(ReconcileLoopAsync);
    }

    private void OnPush(string host, int port, bool bucketScope)
    {
        lock (_lock)
        {
            if (_nodes.TryGetValue(new HostEndpointWithPort(host, port), out var node))
            {
                node.Scopes[bucketScope ? 1 : 0].Wake();
            }
        }
    }

    private async Task ReconcileLoopAsync()
    {
        using var timer = new PeriodicTimer(_interval);
        do
        {
            try
            {
                Reconcile();
            }
            catch (Exception e)
            {
                _recorder.Error("probe", "Could not update the probed node list.", e);
            }
        }
        while (await WaitAsync(timer).ConfigureAwait(false));
    }

    private async Task<bool> WaitAsync(PeriodicTimer timer)
    {
        try
        {
            return await timer.WaitForNextTickAsync(_cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private void Reconcile()
    {
        List<NodeProbe> removed = [];
        lock (_lock)
        {
            var (desired, source) = DesiredEndpoints();
            foreach (var endpoint in desired)
            {
                if (_nodes.ContainsKey(endpoint)) continue;

                var node = new NodeProbe(this, endpoint);
                _nodes.Add(endpoint, node);
                _recorder.Write(Stream, "node-added", new JsonObject
                {
                    ["node"] = endpoint.ToString(),
                    ["source"] = source,
                });
                node.Start(_cts.Token);
            }

            foreach (var (endpoint, node) in _nodes.ToList())
            {
                if (desired.Contains(endpoint)) continue;

                _nodes.Remove(endpoint);
                removed.Add(node);
                _recorder.Write(Stream, "node-removed", new JsonObject
                {
                    ["node"] = endpoint.ToString(),
                    ["source"] = source,
                });
            }
        }

        foreach (var node in removed)
        {
            _ = node.StopAsync();
        }
    }

    /// <summary>
    /// The KV nodes of the newest cluster and bucket configs the probe has seen, or the seeds before that.
    /// </summary>
    private (HashSet<HostEndpointWithPort> Endpoints, string Source) DesiredEndpoints()
    {
        if (_latestCluster is null && _latestBucket is null)
        {
            return (_seeds.ToHashSet(), "seeds");
        }

        var endpoints = new HashSet<HostEndpointWithPort>();
        var sources = new List<string>();
        foreach (var (scope, config) in new[] { ("cluster", _latestCluster), ("bucket", _latestBucket) })
        {
            if (config is null) continue;
            endpoints.UnionWith(KvEndpoints(config));
            sources.Add($"{scope} {config.ConfigVersion}");
        }

        return (endpoints, string.Join(", ", sources));
    }

    private List<HostEndpointWithPort> KvEndpoints(BucketConfig config) =>
        config.GetNodes()
            .Where(n => n.IsKvNode)
            .Select(n => HostEndpointWithPort.Create(n, _options))
            .Where(e => e.Port > 0)
            .Distinct()
            .ToList();

    private void OnConfig(string scope, BucketConfig config, HostEndpointWithPort from)
    {
        lock (_lock)
        {
            if (!_options.HasNetworkResolution && scope == "cluster")
            {
                // The recorded SDK had not decided yet, so decide like the SDK does at bootstrap.
                config.SetEffectiveNetworkResolution(_options);
            }

            var latest = scope == "cluster" ? _latestCluster : _latestBucket;
            if (latest is not null && config.ConfigVersion <= latest.ConfigVersion) return;

            if (scope == "cluster")
            {
                _latestCluster = config;
            }
            else
            {
                _latestBucket = config;
            }

            _recorder.Write(Stream, "latest", new JsonObject
            {
                ["scope"] = scope,
                ["epoch"] = config.ConfigVersion.Epoch,
                ["rev"] = config.ConfigVersion.Revision,
                ["from"] = from.ToString(),
                ["kvNodes"] = new JsonArray(KvEndpoints(config).Select(e => (JsonNode?)JsonValue.Create(e.ToString())).ToArray()),
            });
        }
    }

    public async ValueTask DisposeAsync()
    {
        _tap.ProbePushed -= OnPush;
        await _cts.CancelAsync().ConfigureAwait(false);
        if (_reconciler is not null)
        {
            await _reconciler.ConfigureAwait(false);
        }

        List<NodeProbe> nodes;
        lock (_lock)
        {
            nodes = _nodes.Values.ToList();
            _nodes.Clear();
        }

        await Task.WhenAll(nodes.Select(n => n.StopAsync())).ConfigureAwait(false);
        await _contextCts.CancelAsync().ConfigureAwait(false);
        _context.Dispose();
        _cts.Dispose();
    }

    private sealed class NodeProbe
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly List<Task> _tasks = [];
        private CancellationTokenSource? _linked;

        public NodeProbe(ConfigProbe owner, HostEndpointWithPort endpoint)
        {
            Scopes =
            [
                new ScopeProbe(owner, endpoint, "cluster", null),
                new ScopeProbe(owner, endpoint, "bucket", owner._bucket),
            ];
        }

        public IReadOnlyList<ScopeProbe> Scopes { get; }

        public void Start(CancellationToken probeToken)
        {
            _linked = CancellationTokenSource.CreateLinkedTokenSource(probeToken, _cts.Token);
            var token = _linked.Token;
            foreach (var scope in Scopes)
            {
                _tasks.Add(Task.Run(() => scope.RunAsync(token), CancellationToken.None));
            }
        }

        public async Task StopAsync()
        {
            await _cts.CancelAsync().ConfigureAwait(false);
            await Task.WhenAll(_tasks).ConfigureAwait(false);
            _linked?.Dispose();
            _cts.Dispose();
        }
    }

    private sealed class ScopeProbe(ConfigProbe owner, HostEndpointWithPort endpoint, string scope, string? bucket)
    {
        private const int FailuresBeforeNewNode = 3;
        private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(10);

        private readonly SemaphoreSlim _wake = new(0, 1);
        private IClusterNode? _node;
        private ConfigVersion? _known;
        private bool? _up;
        private long _downSince;
        private int _failuresInRow;
        private long _fetches;
        private long _failures;
        private long _pushWakes;
        private long _lastHeartbeat = Stopwatch.GetTimestamp();

        public bool IsUp => _up == true;

        /// <summary>
        /// Starts the next fetch now. Several wakes before that fetch count as one.
        /// </summary>
        public void Wake()
        {
            try
            {
                if (_wake.CurrentCount == 0)
                {
                    _wake.Release();
                }
            }
            catch (SemaphoreFullException)
            {
            }
        }

        public async Task RunAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await FetchOnceAsync(token).ConfigureAwait(false);
                    Heartbeat();
                    if (await _wake.WaitAsync(owner._interval, token).ConfigureAwait(false))
                    {
                        _pushWakes++;
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception e)
            {
                owner._recorder.Error("probe", $"The {scope} probe of {endpoint} stopped.", e);
            }
            finally
            {
                DropNode();
            }
        }

        private async Task FetchOnceAsync(CancellationToken token)
        {
            try
            {
                if (_node is null)
                {
                    _node = await owner._nodeFactory.CreateAndConnectAsync(endpoint, token).ConfigureAwait(false);
                    if (bucket is not null)
                    {
                        await _node.SelectBucketAsync(bucket, token).ConfigureAwait(false);
                    }
                }

                _fetches++;
                var config = await _node.GetClusterMap(_known, token).ConfigureAwait(false);
                OnAnswer();

                if (config is not null && config.ConfigVersion != _known)
                {
                    var fields = Tags();
                    fields["epoch"] = config.ConfigVersion.Epoch;
                    fields["rev"] = config.ConfigVersion.Revision;
                    fields["prevEpoch"] = _known?.Epoch;
                    fields["prevRev"] = _known?.Revision;
                    fields["name"] = config.Name;
                    owner._recorder.Write(Stream, "version", fields);
                    _known = config.ConfigVersion;
                    try
                    {
                        owner.OnConfig(scope, config, endpoint);
                    }
                    catch (Exception e)
                    {
                        owner._recorder.Error("probe", $"Could not read the node list of {scope} config {config.ConfigVersion}.", e);
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                OnFailure(e);
            }
        }

        private void OnAnswer()
        {
            _failuresInRow = 0;
            if (_up == true) return;

            var fields = Tags();
            if (_up == false)
            {
                fields["downMs"] = Math.Round(Stopwatch.GetElapsedTime(_downSince).TotalMilliseconds, 1);
            }
            else
            {
                fields["first"] = true;
            }

            owner._recorder.Write(Stream, "up", fields);
            _up = true;
        }

        private void OnFailure(Exception e)
        {
            _failures++;
            _failuresInRow++;
            if (_up != false)
            {
                var fields = Tags();
                fields["error"] = e.GetType().Name;
                fields["detail"] = e.Message;
                owner._recorder.Write(Stream, "down", fields);
                _up = false;
                _downSince = Stopwatch.GetTimestamp();
            }

            if (_failuresInRow >= FailuresBeforeNewNode)
            {
                // Start over with a fresh node and connection on the next tick.
                DropNode();
                _failuresInRow = 0;
            }
        }

        private void Heartbeat()
        {
            if (Stopwatch.GetElapsedTime(_lastHeartbeat) < HeartbeatInterval) return;

            var fields = Tags();
            fields["epoch"] = _known?.Epoch;
            fields["rev"] = _known?.Revision;
            fields["up"] = _up == true;
            fields["fetches"] = _fetches;
            fields["pushWakes"] = _pushWakes;
            fields["failures"] = _failures;
            owner._recorder.Write(Stream, "heartbeat", fields);
            _fetches = 0;
            _pushWakes = 0;
            _failures = 0;
            _lastHeartbeat = Stopwatch.GetTimestamp();
        }

        private void DropNode()
        {
            try
            {
                _node?.Dispose();
            }
            catch (Exception e)
            {
                owner._recorder.Error("probe", $"Could not close the {scope} probe node {endpoint}.", e);
            }

            _node = null;
        }

        private JsonObject Tags() => new()
        {
            ["node"] = endpoint.ToString(),
            ["scope"] = scope,
        };
    }
}
