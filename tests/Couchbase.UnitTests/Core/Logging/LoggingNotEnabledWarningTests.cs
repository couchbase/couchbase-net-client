using System;
using System.IO;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

#nullable enable

namespace Couchbase.UnitTests.Core.Logging
{
    public class LoggingNotEnabledWarningTests
    {
        public static TheoryData<string, Func<ILoggerFactory?>> NotEnabled => new()
        {
            { "null factory", () => null },
            { "NullLoggerFactory", () => NullLoggerFactory.Instance },
            { "factory with no providers", () => LoggerFactory.Create(_ => { }) },
            {
                "Couchbase category turned off",
                () => LoggerFactory.Create(b => b.AddProvider(new EnabledProvider()).AddFilter(LoggingNotEnabledWarning.LoggerCategory, LogLevel.None))
            },
            {
                "Couchbase category above Warning",
                () => LoggerFactory.Create(b => b.AddProvider(new EnabledProvider()).AddFilter(LoggingNotEnabledWarning.LoggerCategory, LogLevel.Error))
            },
            {
                "minimum level above Warning",
                () => LoggerFactory.Create(b => b.AddProvider(new EnabledProvider()).SetMinimumLevel(LogLevel.Error))
            },
        };

        public static TheoryData<string, Func<ILoggerFactory?>> Enabled => new()
        {
            { "provider with default levels", () => LoggerFactory.Create(b => b.AddProvider(new EnabledProvider())) },
            {
                "Couchbase category at Warning",
                () => LoggerFactory.Create(b => b.AddProvider(new EnabledProvider()).AddFilter(LoggingNotEnabledWarning.LoggerCategory, LogLevel.Warning))
            },
            {
                "only a child category turned off",
                () => LoggerFactory.Create(b => b.AddProvider(new EnabledProvider()).AddFilter("Couchbase.Core", LogLevel.None))
            },
        };

        [Theory]
        [MemberData(nameof(NotEnabled))]
        public void WarnOnce_LoggingNotEnabled_WritesMessage(string because, Func<ILoggerFactory?> createFactory)
        {
            var writer = new StringWriter();
            var warning = new LoggingNotEnabledWarning(() => writer);

            var wrote = warning.WarnOnce(createFactory());

            Assert.True(wrote, because);
            Assert.Equal(LoggingNotEnabledWarning.Message + writer.NewLine, writer.ToString());
        }

        [Theory]
        [MemberData(nameof(Enabled))]
        public void WarnOnce_LoggingEnabled_WritesNothing(string because, Func<ILoggerFactory?> createFactory)
        {
            var writer = new StringWriter();
            var warning = new LoggingNotEnabledWarning(() => writer);

            var wrote = warning.WarnOnce(createFactory());

            Assert.False(wrote, because);
            Assert.Equal(string.Empty, writer.ToString());
        }

        [Fact]
        public void WarnOnce_SecondCall_WritesNothing()
        {
            var writer = new StringWriter();
            var warning = new LoggingNotEnabledWarning(() => writer);

            Assert.True(warning.WarnOnce(null));
            Assert.False(warning.WarnOnce(null));
            Assert.False(warning.WarnOnce(NullLoggerFactory.Instance));

            Assert.Equal(LoggingNotEnabledWarning.Message + writer.NewLine, writer.ToString());
        }

        [Fact]
        public void WarnOnce_FirstCallEnabled_LaterDisabledCallWritesNothing()
        {
            // The options of the first cluster decide.
            var writer = new StringWriter();
            var warning = new LoggingNotEnabledWarning(() => writer);

            Assert.False(warning.WarnOnce(LoggerFactory.Create(b => b.AddProvider(new EnabledProvider()))));
            Assert.False(warning.WarnOnce(null));

            Assert.Equal(string.Empty, writer.ToString());
        }

        [Fact]
        public void WarnOnce_WriterThrows_DoesNotThrow()
        {
            var warning = new LoggingNotEnabledWarning(() => new ThrowingWriter());

            Assert.True(warning.WarnOnce(null));
        }

        [Fact]
        public void WarnOnce_GetWriterThrows_DoesNotThrow()
        {
            var warning = new LoggingNotEnabledWarning(() => throw new InvalidOperationException("no console"));

            Assert.True(warning.WarnOnce(null));
        }

        [Fact]
        public void WarnOnce_FactoryThrows_DoesNotThrowOrWarn()
        {
            var writer = new StringWriter();
            var warning = new LoggingNotEnabledWarning(() => writer);

            Assert.False(warning.WarnOnce(new ThrowingFactory()));
            Assert.Equal(string.Empty, writer.ToString());
        }

        [Fact]
        public void Message_ContainsDocsUrl()
        {
            Assert.Contains(LoggingNotEnabledWarning.DocsUrl, LoggingNotEnabledWarning.Message);
        }

        private sealed class EnabledProvider : ILoggerProvider
        {
            public ILogger CreateLogger(string categoryName) => new EnabledLogger();

            public void Dispose()
            {
            }
        }

        private sealed class EnabledLogger : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
            }
        }

        private sealed class ThrowingWriter : StringWriter
        {
            public override void WriteLine(string? value) => throw new IOException("stderr is closed");
        }

        private sealed class ThrowingFactory : ILoggerFactory
        {
            public ILogger CreateLogger(string categoryName) => throw new InvalidOperationException("broken factory");

            public void AddProvider(ILoggerProvider provider)
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
