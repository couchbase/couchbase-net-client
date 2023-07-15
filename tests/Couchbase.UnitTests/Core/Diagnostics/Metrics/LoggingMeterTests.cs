using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.UnitTests.Core.Diagnostics.Metrics
{
    public class LoggingMeterTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public LoggingMeterTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public async Task Test_Generate_Report_And_Write_To_Log()
        {
            var loggerFactory = new LoggingMeterTestFactory();
            var meter = new LoggingMeter(loggerFactory, new LoggingMeterOptions().Enabled(true)
                .EmitInterval(TimeSpan.FromMilliseconds(100)));

            var recorder1 = meter.ValueRecorder(OuterRequestSpans.ServiceSpan.Kv.Name);
            var recorder2 = meter.ValueRecorder(OuterRequestSpans.ServiceSpan.N1QLQuery);
            var recorder3 = meter.ValueRecorder(OuterRequestSpans.ServiceSpan.AnalyticsQuery);
            var recorder4 = meter.ValueRecorder(OuterRequestSpans.ServiceSpan.ViewQuery);
            var recorder5 = meter.ValueRecorder(OuterRequestSpans.ServiceSpan.SearchQuery);

            for (var i = 0; i < 1000; i++)
            {
                recorder1.RecordValue(2);
                recorder1.RecordValue(1);
                recorder1.RecordValue(6);

                recorder2.RecordValue(55);
                recorder2.RecordValue(78);
                recorder2.RecordValue(89);
                recorder2.RecordValue(10);

                recorder3.RecordValue(10);
                recorder3.RecordValue(15);
                recorder3.RecordValue(67);

                recorder4.RecordValue(55);
                recorder4.RecordValue(78);
                recorder4.RecordValue(89);
                recorder4.RecordValue(10);

                recorder5.RecordValue(11);
                recorder5.RecordValue(78);
                recorder5.RecordValue(66);
                recorder5.RecordValue(23);
            }

            string actual;
            var attempts = 0;
            var count = 5;
            var maxRetries = 20;
            while ((actual = loggerFactory.LoggedData.FirstOrDefault()) == null && count < maxRetries)
            {
                var sleepTime = TimeSpan.FromMilliseconds(Math.Pow(2, count++));
                _testOutputHelper.WriteLine($"Attempt {attempts++} sleeping for {sleepTime.TotalMilliseconds}ms");
                await Task.Delay(sleepTime);
            }

            Assert.NotNull(actual);
            _testOutputHelper.WriteLine(actual);
        }

        #region Test Logger

        public class LoggingMeterTestFactory : ILoggerFactory
        {
            public ConcurrentBag<string> LoggedData = new();

            public void Dispose()
            {
#if NET6_0_OR_GREATER
                LoggedData.Clear();
#endif
            }

            public ILogger CreateLogger(string categoryName)
            {
                return new LoggingMeterTestsLogger(LoggedData);
            }

            public void AddProvider(ILoggerProvider provider)
            {
                throw new NotImplementedException();
            }

            private class LoggingMeterTestsLogger : ILogger
            {
                private readonly ConcurrentBag<string> _loggedData;

                public LoggingMeterTestsLogger(ConcurrentBag<string> loggedData)
                {
                    _loggedData = loggedData;
                }

                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
                {
                    _loggedData.Add(state.ToString());
                }

                public bool IsEnabled(LogLevel logLevel)
                {
                    throw new NotImplementedException();
                }

                public IDisposable BeginScope<TState>(TState state)
                {
                    throw new NotImplementedException();
                }
            }
        }

        #endregion
    }
}
