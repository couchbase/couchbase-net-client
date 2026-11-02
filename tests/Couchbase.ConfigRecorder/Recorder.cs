using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;

namespace Couchbase.ConfigRecorder;

/// <summary>
/// Writes one JSON line per event to events.jsonl and stores each distinct config body once.
/// Producers never block and never see an exception. One background task does all disk writes.
/// </summary>
internal sealed class Recorder : IAsyncDisposable
{
    private const int ConsoleErrorsPerComponent = 5;

    private abstract record Item;
    private sealed record EventItem(string Stream, string Kind, JsonObject? Fields, long Timestamp, DateTime WallClock) : Item;
    private sealed record ConfigItem(string Hash, byte[] Body) : Item;
    private sealed record FlushItem : Item;

    private readonly long _startTimestamp;
    private readonly Channel<Item> _channel = Channel.CreateUnbounded<Item>(new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentDictionary<string, byte> _hashes = new();
    private readonly ConcurrentDictionary<string, int> _consoleErrors = new();
    private readonly CancellationTokenSource _flushCts = new();
    private readonly string _configsFolder;
    private readonly Task _writer;
    private readonly Task _flusher;
    private long _eventCount;
    private long _writeFailures;

    private Recorder(string runFolder)
    {
        RunFolder = runFolder;
        _configsFolder = Path.Combine(runFolder, "configs");
        Directory.CreateDirectory(_configsFolder);
        _startTimestamp = Stopwatch.GetTimestamp();
        var stream = new FileStream(Path.Combine(runFolder, "events.jsonl"), FileMode.CreateNew, FileAccess.Write, FileShare.Read, 1 << 16);
        _writer = Task.Run(() => WriteLoopAsync(stream));
        _flusher = Task.Run(FlushLoopAsync);
    }

    public string RunFolder { get; }

    public long EventCount => Interlocked.Read(ref _eventCount);

    public static Recorder Create(string outputFolder)
    {
        var name = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var runFolder = Path.GetFullPath(Path.Combine(outputFolder, name));
        Directory.CreateDirectory(runFolder);
        return new Recorder(runFolder);
    }

    public double ElapsedMs(long timestamp) => Stopwatch.GetElapsedTime(_startTimestamp, timestamp).TotalMilliseconds;

    public double ElapsedMs() => ElapsedMs(Stopwatch.GetTimestamp());

    public void Write(string stream, string kind, JsonObject? fields = null) =>
        Write(stream, kind, fields, Stopwatch.GetTimestamp(), DateTime.UtcNow);

    public void Write(string stream, string kind, JsonObject? fields, long timestamp, DateTime wallClock)
    {
        try
        {
            if (_channel.Writer.TryWrite(new EventItem(stream, kind, fields, timestamp, wallClock)))
            {
                Interlocked.Increment(ref _eventCount);
            }
        }
        catch
        {
            // Recording must never break the caller.
        }
    }

    /// <summary>
    /// Records an error of a component. The first few per component also go to the console.
    /// </summary>
    public void Error(string component, string message, Exception? exception = null, bool alwaysConsole = false)
    {
        var fields = new JsonObject
        {
            ["component"] = component,
            ["message"] = message,
        };
        if (exception is not null)
        {
            fields["error"] = exception.GetType().Name;
            fields["detail"] = exception.Message;
        }

        Write("recorder", "error", fields);

        var count = alwaysConsole ? 0 : _consoleErrors.AddOrUpdate(component, 1, (_, c) => c + 1);
        if (count <= ConsoleErrorsPerComponent)
        {
            var suffix = exception is null ? "" : $" ({exception.GetType().Name}, {exception.Message})";
            Console.Error.WriteLine($"[{component}] {message}{suffix}");
            if (count == ConsoleErrorsPerComponent)
            {
                Console.Error.WriteLine($"[{component}] Further errors go to events.jsonl only.");
            }
        }
    }

    /// <summary>
    /// Stores a config body as configs/&lt;sha256&gt;.json the first time it is seen and returns the hash.
    /// </summary>
    public string StoreConfig(ReadOnlySpan<byte> body)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(body));
        if (_hashes.TryAdd(hash, 0))
        {
            _channel.Writer.TryWrite(new ConfigItem(hash, body.ToArray()));
        }

        return hash;
    }

    private async Task FlushLoopAsync()
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
            while (await timer.WaitForNextTickAsync(_flushCts.Token).ConfigureAwait(false))
            {
                _channel.Writer.TryWrite(new FlushItem());
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task WriteLoopAsync(FileStream stream)
    {
        var buffer = new ArrayBufferWriter<byte>(4096);
        // Relaxed escaping keeps characters such as + readable. Lines go to a file, never to HTML.
        await using var json = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        try
        {
            await foreach (var item in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                try
                {
                    switch (item)
                    {
                        case EventItem e:
                            buffer.ResetWrittenCount();
                            json.Reset(buffer);
                            WriteEvent(json, e);
                            json.Flush();
                            buffer.Write("\n"u8);
                            stream.Write(buffer.WrittenSpan);
                            break;
                        case ConfigItem c:
                            var path = Path.Combine(_configsFolder, c.Hash + ".json");
                            if (!File.Exists(path))
                            {
                                await File.WriteAllBytesAsync(path, c.Body).ConfigureAwait(false);
                            }

                            break;
                        case FlushItem:
                            stream.Flush();
                            break;
                    }
                }
                catch (Exception e)
                {
                    if (Interlocked.Increment(ref _writeFailures) <= ConsoleErrorsPerComponent)
                    {
                        Console.Error.WriteLine($"[recorder] Write failed ({e.GetType().Name}, {e.Message})");
                    }
                }
            }
        }
        finally
        {
            try
            {
                stream.Flush();
                await stream.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"[recorder] Closing events.jsonl failed ({e.GetType().Name}, {e.Message})");
            }
        }
    }

    private void WriteEvent(Utf8JsonWriter json, EventItem e)
    {
        json.WriteStartObject();
        json.WriteString("ts", e.WallClock.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture));
        json.WriteNumber("t", Math.Round(ElapsedMs(e.Timestamp), 3));
        json.WriteString("stream", e.Stream);
        json.WriteString("kind", e.Kind);
        if (e.Fields is not null)
        {
            foreach (var (name, value) in e.Fields)
            {
                json.WritePropertyName(name);
                if (value is null)
                {
                    json.WriteNullValue();
                }
                else
                {
                    value.WriteTo(json);
                }
            }
        }

        json.WriteEndObject();
    }

    public async ValueTask DisposeAsync()
    {
        await _flushCts.CancelAsync().ConfigureAwait(false);
        await _flusher.ConfigureAwait(false);
        _channel.Writer.TryComplete();
        await _writer.ConfigureAwait(false);
        _flushCts.Dispose();
    }
}
