using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
        public void Test_Generate_Report_And_Write_To_Log()
        {
            var loggerFactory = new LoggingMeterTestFactory();
            var meter = new LoggingMeter(loggerFactory, new LoggingMeterOptions().Enabled(true)
                .EmitInterval(TimeSpan.FromSeconds(2)));

            var recorder1 = meter.ValueRecorder($"{OuterRequestSpans.ServiceSpan.Kv.Name}|localhost");
            var recorder2 = meter.ValueRecorder($"{OuterRequestSpans.ServiceSpan.N1QLQuery}|127.0.0.1");
            var recorder3 = meter.ValueRecorder($"{OuterRequestSpans.ServiceSpan.Kv.Name}|210.0.0.1");
            var recorder4 = meter.ValueRecorder($"{OuterRequestSpans.ServiceSpan.N1QLQuery}|10.112.211.101");

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
            }

            Thread.Sleep(3000);

            var actual = loggerFactory.LoggedData.FirstOrDefault();

            Assert.NotNull(actual);
            _testOutputHelper.WriteLine(actual);
        }

        #region Test Logger

        public class LoggingMeterTestFactory : ILoggerFactory
        {
            public ConcurrentBag<string> LoggedData = new();

            public void Dispose()
            {
                LoggedData.Clear();
            }

            public ILogger CreateLogger(string categoryName)
            {
                return new AggregatingMeterTestsLogger(LoggedData);
            }

            public void AddProvider(ILoggerProvider provider)
            {
                throw new NotImplementedException();
            }

            private class AggregatingMeterTestsLogger : ILogger
            {
                private readonly ConcurrentBag<string> _loggedData;

                public AggregatingMeterTestsLogger(ConcurrentBag<string> loggedData)
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
