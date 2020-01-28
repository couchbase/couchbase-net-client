using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Couchbase.UnitTests.Core.Logging
{
    public class LogManagerTests
    {
        [Fact(Skip = "Skip due to known design flaw, see NCBC-2324")]
        public void Test_LogLevel_Debug()
        {
            //arrange
            var loggerProvider = new InMemoryLoggerProvider();
            LogManager.LoggerFactory.AddProvider(loggerProvider);

            var poco = new Poco();

            //act
            poco.DebugLogSomething();

            // wait to make sure the log entry is added to the logger
            Thread.Sleep(50);

            //assert
            Assert.Contains("Something happened.", loggerProvider.Logs["Couchbase.UnitTests.Core.Logging.LogManagerTests.Poco"][0]);
        }

        private class Poco
        {
            private static ILogger Logger { get; } = LogManager.CreateLogger<Poco>();

            public void DebugLogSomething()
            {
                Logger.LogDebug("Something happened. {0}", "whooh");
            }
        }

        public class InMemoryLogger : ILogger
        {
            private readonly InMemoryLoggerProvider _provider;
            private readonly string _categoryName;

            public InMemoryLogger(InMemoryLoggerProvider provider, string categoryName)
            {
                _provider = provider;
                _categoryName = categoryName;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                var msg = $"{DateTime.Now} [{Thread.CurrentThread.ManagedThreadId}] {logLevel} {formatter(state, exception)}";
                _provider.Loggers.TryAdd(_categoryName, this);
                _provider.Logs.TryAdd(_categoryName, new List<string>());
                _provider.Logs[_categoryName].Add(msg);
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public IDisposable BeginScope<TState>(TState state)
            {
                throw new NotImplementedException();
            }
        }

        public class InMemoryLoggerProvider : ILoggerProvider
        {
            public readonly ConcurrentDictionary<string, List<string>> Logs = new ConcurrentDictionary<string, List<string>>();
            public readonly ConcurrentDictionary<string, InMemoryLogger> Loggers = new ConcurrentDictionary<string, InMemoryLogger>();

            public void Dispose()
            {
                Loggers.Clear();
            }

            public ILogger CreateLogger(string categoryName)
            {
                Logs.TryAdd(categoryName, new List<string>());

                var logger = new InMemoryLogger(this, categoryName);
                Loggers.TryAdd(categoryName, logger);

                return logger;
            }
        }
    }
}
