using System;
using System.IO;
using System.Threading.Tasks;
using Couchbase.Core.DI;
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

        // Cluster tests. Each registers its own warning instance instead of the process-wide one, so they do not
        // depend on, or use up, the once-per-process state, and they do not capture the real stderr.

        [Fact]
        public void Cluster_TwoClustersWithoutLogging_WarnsOnce()
        {
            var writer = new StringWriter();
            var warning = new LoggingNotEnabledWarning(() => writer);

            using var first = new Cluster(CreateOptions(warning));
            using var second = new Cluster(CreateOptions(warning));

            Assert.Equal(LoggingNotEnabledWarning.Message + writer.NewLine, writer.ToString());
        }

        [Fact]
        public void Cluster_FirstClusterHasLogging_NoWarningForLaterClusters()
        {
            var writer = new StringWriter();
            var warning = new LoggingNotEnabledWarning(() => writer);
            using var loggerFactory = LoggerFactory.Create(b => b.AddProvider(new EnabledProvider()));

            using var first = new Cluster(CreateOptions(warning).WithLogging(loggerFactory));
            using var second = new Cluster(CreateOptions(warning));

            Assert.Equal(string.Empty, writer.ToString());
        }

        [Fact]
        public void Cluster_DefaultService_IsTheProcessWideInstance()
        {
            // A new instance per cluster would warn once per cluster, not once per process.
            using var loggerFactory = LoggerFactory.Create(b => b.AddProvider(new EnabledProvider()));
            using var cluster = new Cluster(new ClusterOptions()
                .WithConnectionString("couchbase://localhost")
                .WithPasswordAuthentication("username", "password")
                .WithLogging(loggerFactory));

            Assert.Same(LoggingNotEnabledWarning.Instance, cluster.ClusterServices.GetRequiredService<LoggingNotEnabledWarning>());
        }

#if NETCOREAPP3_1_OR_GREATER
        [Fact]
        public async Task ConnectAsync_Couchbase2WithoutLogging_Warns()
        {
            // couchbase2:// creates a StellarCluster, which connects lazily, so no server is needed.
            var writer = new StringWriter();
            var options = CreateOptions(new LoggingNotEnabledWarning(() => writer));

            var cluster = await Cluster.ConnectAsync("couchbase2://127.0.0.1:1", options);
            await cluster.DisposeAsync();

            Assert.Equal(LoggingNotEnabledWarning.Message + writer.NewLine, writer.ToString());
        }
#endif

        private static ClusterOptions CreateOptions(LoggingNotEnabledWarning warning) =>
            new ClusterOptions()
                .WithConnectionString("couchbase://localhost")
                .WithPasswordAuthentication("username", "password")
                .AddClusterService(warning);

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
