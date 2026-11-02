using System.Globalization;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Couchbase.ConfigRecorder.Sdk;

/// <summary>
/// Writes log lines to one file from a background task and flushes every second.
/// </summary>
internal sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly Channel<string> _lines = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
    private readonly LogLevel _minimumLevel;
    private readonly Func<string, EventId, object?, bool> _filter;
    private readonly Task _writer;

    public FileLoggerProvider(string path, LogLevel minimumLevel, Func<string, EventId, object?, bool> filter)
    {
        _minimumLevel = minimumLevel;
        _filter = filter;
        var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 1 << 16);
        _writer = Task.Run(() => WriteLoopAsync(new StreamWriter(stream, new UTF8Encoding(false))));
    }

    public static ILoggerFactory CreateFactory(string path, LogLevel minimumLevel, Func<string, EventId, object?, bool>? filter = null)
    {
        var provider = new FileLoggerProvider(path, minimumLevel, filter ?? ((_, _, _) => true));
        return LoggerFactory.Create(builder => builder.SetMinimumLevel(minimumLevel).AddProvider(provider));
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    private async Task WriteLoopAsync(StreamWriter writer)
    {
        await using (writer.ConfigureAwait(false))
        {
            var nextFlush = DateTime.UtcNow.AddSeconds(1);
            while (true)
            {
                var wait = nextFlush - DateTime.UtcNow;
                bool more;
                try
                {
                    using var cts = new CancellationTokenSource(wait > TimeSpan.Zero ? wait : TimeSpan.Zero);
                    more = await _lines.Reader.WaitToReadAsync(cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    more = true;
                }

                try
                {
                    while (_lines.Reader.TryRead(out var line))
                    {
                        await writer.WriteLineAsync(line).ConfigureAwait(false);
                    }

                    if (!more)
                    {
                        await writer.FlushAsync().ConfigureAwait(false);
                        return;
                    }

                    if (DateTime.UtcNow >= nextFlush)
                    {
                        await writer.FlushAsync().ConfigureAwait(false);
                        nextFlush = DateTime.UtcNow.AddSeconds(1);
                    }
                }
                catch (IOException e)
                {
                    Console.Error.WriteLine($"[log] Write failed ({e.Message})");
                }
            }
        }
    }

    public void Dispose()
    {
        _lines.Writer.TryComplete();
        _writer.Wait(TimeSpan.FromSeconds(5));
    }

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= provider._minimumLevel && logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel) || !provider._filter(category, eventId, state)) return;

            try
            {
                var builder = new StringBuilder(256);
                builder.Append(DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture))
                    .Append(' ').Append(ShortLevel(logLevel))
                    .Append(' ').Append(category);
                if (eventId.Id != 0)
                {
                    builder.Append('[').Append(eventId.Id).Append(']');
                }

                builder.Append(' ').Append(formatter(state, exception));
                if (exception is not null)
                {
                    builder.Append(' ').Append(exception);
                }

                provider._lines.Writer.TryWrite(builder.ToString());
            }
            catch
            {
                // Logging must never break the SDK.
            }
        }

        private static string ShortLevel(LogLevel level) => level switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            _ => "???",
        };
    }
}
