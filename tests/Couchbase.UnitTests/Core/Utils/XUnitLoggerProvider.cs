using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Couchbase.UnitTests.Core.Utils
{
    public class XUnitLoggerProvider : ILoggerProvider
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private int _disposed;

        public XUnitLoggerProvider(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        private void WriteLine(string message)
        {
            // Some async tasks may continue after the test is complete, if they create logs then it will cause the
            // test to fail with "An error occurred while writing to logger(s). (There is no currently active test.)".
            // So long as the tests dispose of their ILoggerFactory after each test, this logic will ensure that no
            // further logs are written after the test completes.

            if (Volatile.Read(ref _disposed) == 0)
            {
                _testOutputHelper.WriteLine(message);
            }
        }

        public ILogger CreateLogger(string categoryName)
            => new XUnitLogger(this, categoryName);

        public void Dispose()
        {
            Volatile.Write(ref _disposed, 1);
        }

        private class XUnitLogger : ILogger
        {
            private readonly XUnitLoggerProvider _provider;
            private readonly string _categoryName;

            public XUnitLogger(XUnitLoggerProvider provider, string categoryName)
            {
                _provider = provider;
                _categoryName = categoryName;
            }

            public IDisposable BeginScope<TState>(TState state) => NoopDisposable.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                _provider.WriteLine($"{_categoryName} [{eventId}] {formatter(state, exception)}");
                if (exception != null)
                    _provider.WriteLine(exception.ToString());
            }

            private class NoopDisposable : IDisposable
            {
                public static NoopDisposable Instance = new NoopDisposable();

                public void Dispose()
                {
                }
            }
        }
    }
}
