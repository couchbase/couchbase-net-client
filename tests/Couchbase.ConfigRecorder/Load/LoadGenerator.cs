using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Nodes;
using Couchbase.ConfigRecorder.Tap;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Transcoders;
using Couchbase.KeyValue;

namespace Couchbase.ConfigRecorder.Load;

/// <summary>
/// Closed-loop workers with no rate limit. Each picks a random key, then upserts or gets it.
/// </summary>
internal sealed class LoadGenerator : IAsyncDisposable
{
    private const string Stream = "load";
    private static readonly TimeSpan ErrorPause = TimeSpan.FromMilliseconds(10);

    private readonly Recorder _recorder;
    private readonly TapHub _tap;
    private readonly ICouchbaseCollection _collection;
    private readonly LoadSettings _settings;
    private readonly byte[] _value;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<string, long> _errors = new();
    private readonly List<Task> _tasks = [];
    private long _upserts;
    private long _gets;
    private long _misses;
    private long _maxLatencyTicks;

    public LoadGenerator(Recorder recorder, TapHub tap, ICouchbaseCollection collection, LoadSettings settings)
    {
        _recorder = recorder;
        _tap = tap;
        _collection = collection;
        _settings = settings;
        _value = new byte[settings.ValueSize];
        Random.Shared.NextBytes(_value);
    }

    public long LastOpsPerSecond { get; private set; }

    public long LastErrors { get; private set; }

    public long LastNotMyVBucket { get; private set; }

    public void Start()
    {
        _recorder.Write(Stream, "start", new JsonObject
        {
            ["workers"] = _settings.Workers,
            ["keyCount"] = _settings.KeyCount,
            ["valueSize"] = _settings.ValueSize,
        });

        for (var i = 0; i < _settings.Workers; i++)
        {
            _tasks.Add(Task.Run(WorkerAsync));
        }

        _tasks.Add(Task.Run(StatsLoopAsync));
    }

    private async Task WorkerAsync()
    {
        var upsertOptions = new UpsertOptions().Transcoder(new RawBinaryTranscoder());
        var getOptions = new GetOptions().Transcoder(new RawBinaryTranscoder());
        var token = _cts.Token;
        while (!token.IsCancellationRequested)
        {
            var key = "cr-" + Random.Shared.Next(_settings.KeyCount).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var upsert = Random.Shared.Next(2) == 0;
            var started = Stopwatch.GetTimestamp();
            try
            {
                if (upsert)
                {
                    await _collection.UpsertAsync(key, _value, upsertOptions).ConfigureAwait(false);
                    Interlocked.Increment(ref _upserts);
                }
                else
                {
                    using var result = await _collection.GetAsync(key, getOptions).ConfigureAwait(false);
                    Interlocked.Increment(ref _gets);
                }

                TrackLatency(started);
            }
            catch (DocumentNotFoundException)
            {
                Interlocked.Increment(ref _misses);
                TrackLatency(started);
            }
            catch (Exception e)
            {
                _errors.AddOrUpdate(e.GetType().Name, 1, (_, n) => n + 1);
                if (token.IsCancellationRequested) return;
                try
                {
                    await Task.Delay(ErrorPause, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private void TrackLatency(long started)
    {
        var elapsed = Stopwatch.GetTimestamp() - started;
        var current = Interlocked.Read(ref _maxLatencyTicks);
        while (elapsed > current)
        {
            var seen = Interlocked.CompareExchange(ref _maxLatencyTicks, elapsed, current);
            if (seen == current) break;
            current = seen;
        }
    }

    private async Task StatsLoopAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        var last = Stopwatch.GetTimestamp();
        try
        {
            while (await timer.WaitForNextTickAsync(_cts.Token).ConfigureAwait(false))
            {
                last = WriteStats(last);
            }
        }
        catch (OperationCanceledException)
        {
        }

        WriteStats(last);
    }

    private long WriteStats(long last)
    {
        try
        {
            var now = Stopwatch.GetTimestamp();
            var seconds = Math.Max(Stopwatch.GetElapsedTime(last, now).TotalSeconds, 0.001);
            var upserts = Interlocked.Exchange(ref _upserts, 0);
            var gets = Interlocked.Exchange(ref _gets, 0);
            var misses = Interlocked.Exchange(ref _misses, 0);
            var maxTicks = Interlocked.Exchange(ref _maxLatencyTicks, 0);
            var nmvb = _tap.TakeSdkNotMyVBucket();

            var errors = new JsonObject();
            long errorCount = 0;
            foreach (var type in _errors.Keys.ToList())
            {
                if (_errors.TryRemove(type, out var n) && n > 0)
                {
                    errors[type] = n;
                    errorCount += n;
                }
            }

            var ops = upserts + gets + misses;
            LastOpsPerSecond = (long)(ops / seconds);
            LastErrors = errorCount;
            LastNotMyVBucket = nmvb;

            _recorder.Write(Stream, "stats", new JsonObject
            {
                ["seconds"] = Math.Round(seconds, 3),
                ["ops"] = ops,
                ["upserts"] = upserts,
                ["gets"] = gets,
                ["misses"] = misses,
                ["errors"] = errors,
                ["nmvb"] = nmvb,
                ["maxMs"] = Math.Round(maxTicks * 1000.0 / Stopwatch.Frequency, 3),
            }, now, DateTime.UtcNow);
            return now;
        }
        catch (Exception e)
        {
            _recorder.Error("load", "Could not write load stats.", e);
            return Stopwatch.GetTimestamp();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        var all = Task.WhenAll(_tasks);
        if (await Task.WhenAny(all, Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(false) != all)
        {
            _recorder.Error("load", "Some workers did not stop within 10 s.");
        }

        _recorder.Write(Stream, "stop");
    }
}
