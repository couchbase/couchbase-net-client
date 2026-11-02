using System.Text.Json.Nodes;
using DnsClient;

namespace Couchbase.ConfigRecorder.DnsSrv;

/// <summary>
/// Resolves the DNS SRV records of the connection string and records when the answer changes.
/// </summary>
internal sealed class DnsSrvWatcher : IAsyncDisposable
{
    private const string Stream = "dns";

    private readonly Recorder _recorder;
    private readonly TimeSpan _interval;
    private readonly string? _query;
    private readonly string? _skipReason;
    private readonly LookupClient _lookup = new(new LookupClientOptions { UseCache = false });
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;
    private string? _last;

    public DnsSrvWatcher(Recorder recorder, string connectionString, TimeSpan interval)
    {
        _recorder = recorder;
        _interval = interval;

        var parsed = ConnectionString.Parse(connectionString);
        if (!parsed.IsValidDnsSrv())
        {
            _skipReason = "The connection string has more than one host or a port, so the SDK does not use DNS SRV.";
            return;
        }

        // Same name as the SDK DnsClientDnsResolver.
        var uri = parsed.GetDnsBootStrapUri();
        _query = (uri.Scheme == "couchbases" ? "_couchbases._tcp." : "_couchbase._tcp.") + uri.Host;
    }

    public void Start()
    {
        if (_query is null)
        {
            _recorder.Write(Stream, "skipped", new JsonObject { ["reason"] = _skipReason });
            return;
        }

        _recorder.Write(Stream, "start", new JsonObject
        {
            ["query"] = _query,
            ["intervalMs"] = _interval.TotalMilliseconds,
        });
        _loop = Task.Run(LoopAsync);
    }

    private async Task LoopAsync()
    {
        using var timer = new PeriodicTimer(_interval);
        do
        {
            try
            {
                await ResolveAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                Record("error", new JsonObject
                {
                    ["query"] = _query,
                    ["error"] = e.GetType().Name,
                    ["detail"] = e.Message,
                }, $"exception {e.GetType().Name} {e.Message}");
            }
        }
        while (await WaitAsync(timer).ConfigureAwait(false));
    }

    private async Task ResolveAsync()
    {
        var result = await _lookup.QueryAsync(_query!, QueryType.SRV, cancellationToken: _cts.Token).ConfigureAwait(false);
        if (result.HasError)
        {
            Record("error", new JsonObject
            {
                ["query"] = _query,
                ["error"] = result.ErrorMessage,
            }, $"error {result.ErrorMessage}");
            return;
        }

        var records = result.Answers.SrvRecords()
            .OrderBy(r => r.Priority).ThenBy(r => r.Target.Value, StringComparer.Ordinal).ThenBy(r => r.Port)
            .ToList();
        var signature = string.Join(";", records.Select(r => $"{r.Target.Value}:{r.Port}/{r.Priority}/{r.Weight}"));
        var array = new JsonArray();
        foreach (var r in records)
        {
            array.Add(new JsonObject
            {
                ["target"] = r.Target.Value.TrimEnd('.'),
                ["port"] = r.Port,
                ["priority"] = r.Priority,
                ["weight"] = r.Weight,
                ["ttl"] = r.InitialTimeToLive,
            });
        }

        Record("records", new JsonObject
        {
            ["query"] = _query,
            ["count"] = records.Count,
            ["records"] = array,
        }, "records " + signature);
    }

    private void Record(string kind, JsonObject fields, string signature)
    {
        if (signature == _last) return;
        _last = signature;
        _recorder.Write(Stream, kind, fields);
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

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        if (_loop is not null)
        {
            await _loop.ConfigureAwait(false);
        }

        _cts.Dispose();
    }
}
