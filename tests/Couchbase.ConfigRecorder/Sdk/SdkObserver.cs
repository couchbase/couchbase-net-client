using System.Text.Json.Nodes;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;

namespace Couchbase.ConfigRecorder.Sdk;

/// <summary>
/// Records what the recorded SDK instance does with cluster maps. It only reads SDK state.
/// </summary>
internal sealed class SdkObserver : IConfigUpdateEventSink, IAsyncDisposable
{
    private readonly Recorder _recorder;
    private readonly BucketBase _bucket;
    private readonly ClusterContext _context;
    private readonly IConfigHandler _configHandler;
    private readonly TimeSpan _interval;
    private readonly CancellationTokenSource _cts = new();
    private Task? _sampler;
    private string? _lastState;

    public SdkObserver(Recorder recorder, BucketBase bucket, TimeSpan interval)
    {
        _recorder = recorder;
        _bucket = bucket;
        _context = bucket.Context;
        _configHandler = (IConfigHandler)(_context.ServiceProvider.GetService(typeof(IConfigHandler))
            ?? throw new InvalidOperationException("The SDK has no config handler."));
        _interval = interval;
    }

    /// <summary>
    /// A name no bucket can have, so the handler keeps this sink apart from the buckets.
    /// </summary>
    public string Name => "__config_recorder__";

    public IEnumerable<IClusterNode> ClusterNodes => [];

    public string? LatestBucketVersion { get; private set; }

    public void Start()
    {
        _configHandler.Subscribe(this);
        _sampler = Task.Run(SampleLoopAsync);
    }

    public Task ConfigUpdatedAsync(BucketConfig newConfig)
    {
        try
        {
            _recorder.Write("sdk", "dispatch", new JsonObject
            {
                ["name"] = newConfig.Name,
                ["epoch"] = newConfig.ConfigVersion.Epoch,
                ["rev"] = newConfig.ConfigVersion.Revision,
                ["ignoreRev"] = newConfig.IgnoreRev,
                ["nodesExt"] = newConfig.NodesExt?.Count,
            });
        }
        catch (Exception e)
        {
            _recorder.Error("sdk", "Could not record a dispatched config.", e);
        }

        return Task.CompletedTask;
    }

    private async Task SampleLoopAsync()
    {
        using var timer = new PeriodicTimer(_interval);
        do
        {
            try
            {
                Sample();
            }
            catch (Exception e)
            {
                _recorder.Error("sdk", "Could not sample the SDK state.", e);
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

    private void Sample()
    {
        var global = _context.GlobalConfig;
        var current = _bucket.CurrentConfig;
        var keyMapper = _bucket.KeyMapper;

        var nodes = new JsonArray();
        foreach (var node in _context.Nodes.OrderBy(n => n.EndPoint.ToString()).ThenBy(n => n.BucketName))
        {
            var adapterVersion = node.NodesAdapter?.ConfigVersion;
            nodes.Add(new JsonObject
            {
                ["endpoint"] = node.EndPoint.ToString(),
                ["owner"] = node.Owner?.Name,
                ["epoch"] = adapterVersion?.Epoch,
                ["rev"] = adapterVersion?.Revision,
                ["services"] = Services(node),
            });
        }

        var state = new JsonObject
        {
            ["globalEpoch"] = global?.ConfigVersion.Epoch,
            ["globalRev"] = global?.ConfigVersion.Revision,
            ["clusterCapabilities"] = Capabilities(global),
            ["clusterCapabilitiesVer"] = global?.ClusterCapabilitiesVersion is { } ver
                ? new JsonArray(ver.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray())
                : null,
            ["bucketEpoch"] = current?.ConfigVersion.Epoch,
            ["bucketRev"] = current?.ConfigVersion.Revision,
            ["keyMapperRev"] = keyMapper?.Rev,
            ["nodes"] = nodes,
        };

        var text = state.ToJsonString();
        if (text == _lastState) return;

        _lastState = text;
        if (current is not null)
        {
            LatestBucketVersion = current.ConfigVersion.ToString();
        }

        _recorder.Write("sdk", "state", state);
    }

    private static JsonArray Services(IClusterNode node)
    {
        var services = new JsonArray();
        if (node.HasKv) services.Add("kv");
        if (node.HasQuery) services.Add("n1ql");
        if (node.HasSearch) services.Add("fts");
        if (node.HasAnalytics) services.Add("cbas");
        if (node.HasViews) services.Add("views");
        if (node.HasEventing) services.Add("eventing");
        if (node.HasManagement) services.Add("mgmt");
        return services;
    }

    private static JsonObject? Capabilities(BucketConfig? config)
    {
        if (config?.ClusterCapabilities is null) return null;

        var result = new JsonObject();
        foreach (var (service, caps) in config.ClusterCapabilities.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            result[service] = new JsonArray(caps.Select(c => (JsonNode?)JsonValue.Create(c)).ToArray());
        }

        return result;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _configHandler.Unsubscribe(this);
        }
        catch (Exception e)
        {
            _recorder.Error("sdk", "Could not unsubscribe from the config handler.", e);
        }

        await _cts.CancelAsync().ConfigureAwait(false);
        if (_sampler is not null)
        {
            await _sampler.ConfigureAwait(false);
        }

        _cts.Dispose();
    }
}
