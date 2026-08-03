#if NET6_0_OR_GREATER

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.UnitTests.Core.IO.Authentication.X509.Helpers;

/// <summary>
/// Captures every log call the validator makes, whether or not the level is enabled, so a test can assert
/// on which branch it took.
/// </summary>
internal sealed class RecordingLogger : ILogger
{
    private readonly bool _debugEnabled;
    private readonly List<string> _messages = new();

    public RecordingLogger(bool debugEnabled) => _debugEnabled = debugEnabled;

    public IReadOnlyList<string> Messages => _messages;

    // Trace stays off so the per-certificate dumps do not drown the messages under test.
    public bool IsEnabled(LogLevel logLevel) =>
        logLevel != LogLevel.Trace && (_debugEnabled || logLevel > LogLevel.Debug);

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) => _messages.Add(formatter(state, exception));

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}

#endif
