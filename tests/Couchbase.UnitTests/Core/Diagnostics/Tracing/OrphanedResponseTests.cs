using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Diagnostics.Tracing.OrphanResponseReporting;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.UnitTests.Core.Diagnostics.Tracing
{
    /// <summary>
    /// The reporter's two background loops are a sleep around <c>DrainQueue</c> and <c>EmitSummary</c>,
    /// so these tests build it without starting them and drive those two steps directly. Everything
    /// then happens on the test's own thread: there is no clock to wait on, no scheduler to depend on,
    /// and nothing to poll — and what is asserted is what the reporter actually reports, which the
    /// timing-based test this replaced never got as far as checking (NCBC-4293).
    /// </summary>
    public class OrphanedResponseTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public OrphanedResponseTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void Each_Service_Is_Reported_Under_Its_Own_Name()
        {
            var logger = new CapturingLogger();
            var reporter = CreateReporter(logger);

            reporter.Add(GetOrphanSummary(OuterRequestSpans.ServiceSpan.Kv.Name));
            reporter.Add(GetOrphanSummary(OuterRequestSpans.ServiceSpan.N1QLQuery));
            reporter.Add(GetOrphanSummary(OuterRequestSpans.ServiceSpan.Kv.Name));
            reporter.Add(GetOrphanSummary(OuterRequestSpans.ServiceSpan.ViewQuery));

            reporter.DrainQueue();
            reporter.EmitSummary();

            var report = Assert.Single(logger.Reports);
            _testOutputHelper.WriteLine(report);

            using var parsed = JsonDocument.Parse(report);
            Assert.Equal(2u, TotalCountFor(parsed, OuterRequestSpans.ServiceSpan.Kv.Name));
            Assert.Equal(1u, TotalCountFor(parsed, OuterRequestSpans.ServiceSpan.N1QLQuery));
            Assert.Equal(1u, TotalCountFor(parsed, OuterRequestSpans.ServiceSpan.ViewQuery));

            // A service with no orphans is left out of the report rather than reported as empty.
            Assert.False(parsed.RootElement.TryGetProperty(OuterRequestSpans.ServiceSpan.SearchQuery, out _));
            Assert.False(parsed.RootElement.TryGetProperty(OuterRequestSpans.ServiceSpan.AnalyticsQuery, out _));
        }

        [Fact]
        public void Every_Orphan_Is_Counted_But_Only_SampleSize_Are_Reported()
        {
            var logger = new CapturingLogger();
            var reporter = CreateReporter(logger, sampleSize: 2);

            for (var i = 0; i < 5; i++)
            {
                reporter.Add(GetOrphanSummary(OuterRequestSpans.ServiceSpan.Kv.Name));
            }

            reporter.DrainQueue();

            // The count is of everything seen, not of what was kept — that is the point of sampling.
            Assert.Equal(5u, reporter.TotalCount);

            reporter.EmitSummary();

            using var parsed = JsonDocument.Parse(Assert.Single(logger.Reports));
            Assert.Equal(5u, TotalCountFor(parsed, OuterRequestSpans.ServiceSpan.Kv.Name));
            Assert.Equal(2, TopRequestsFor(parsed, OuterRequestSpans.ServiceSpan.Kv.Name));
        }

        [Fact]
        public void Reported_Orphans_Are_Not_Reported_Again()
        {
            var logger = new CapturingLogger();
            var reporter = CreateReporter(logger);

            reporter.Add(GetOrphanSummary(OuterRequestSpans.ServiceSpan.Kv.Name));
            reporter.DrainQueue();
            reporter.EmitSummary();

            Assert.Single(logger.Reports);

            // Emitting again with nothing new must stay silent, and the counts must have been reset
            // with the report rather than accumulating into the next one.
            reporter.EmitSummary();

            Assert.Single(logger.Reports);
            Assert.Equal(0u, reporter.TotalCount);
        }

        [Fact]
        public void Nothing_Is_Reported_When_There_Are_No_Orphans()
        {
            var logger = new CapturingLogger();
            var reporter = CreateReporter(logger);

            reporter.DrainQueue();
            reporter.EmitSummary();

            Assert.Empty(logger.Reports);
        }

        [Fact]
        public void An_Unrecognised_Service_Is_Logged_And_Not_Reported()
        {
            var logger = new CapturingLogger();
            var reporter = CreateReporter(logger);

            reporter.Add(GetOrphanSummary("not-a-service"));

            reporter.DrainQueue();
            reporter.EmitSummary();

            Assert.Equal(0u, reporter.TotalCount);
            Assert.Empty(logger.Reports);
            Assert.Contains(logger.Entries, entry => entry.Message.Contains("not-a-service"));
        }

        /// <summary>
        /// Built without its background loops, so the queue drain and the summary happen only when this
        /// test says so.
        /// </summary>
        private static OrphanReporter CreateReporter(ILogger<OrphanReporter> logger, uint sampleSize = 10) =>
            new(logger, new OrphanOptions { SampleSize = sampleSize }, startProcessing: false);

        private static uint TotalCountFor(JsonDocument report, string serviceName) =>
            report.RootElement.GetProperty(serviceName).GetProperty("total_count").GetUInt32();

        private static int TopRequestsFor(JsonDocument report, string serviceName) =>
            report.RootElement.GetProperty(serviceName).GetProperty("top_requests").GetArrayLength();

        private OrphanSummary GetOrphanSummary(string serviceType)
        {
            return new()
            {
                ServiceType = serviceType,
                total_duration_us = 1200,
                encode_duration_us = 100,
                last_dispatch_duration_us = 40,
                total_dispatch_duration_us = 40,
                last_server_duration_us = 2,
                total_server_duration_us = 2,
                timeout_ms = 75000, operation_name = "upsert",
                last_local_id = "66388CF5BFCF7522/18CC8791579B567C",
                operation_id = "0x23",
                last_local_socket = "10.211.55.3:52450",
                last_remote_socket = "10.112.180.101:11210"
            };
        }

        /// <summary>
        /// Records what the reporter logs. No synchronisation: with the background loops not started,
        /// every entry is written by the test's own thread.
        /// </summary>
        private sealed class CapturingLogger : ILogger<OrphanReporter>
        {
            /// <summary>
            /// The event id <c>LogOrphanedResponses</c> writes the orphan summary under.
            /// </summary>
            private const int OrphansObserved = 100;

            public List<(int EventId, string Message)> Entries { get; } = new();

            /// <summary>
            /// The summaries reported, as the JSON the reporter serialised — the log message is a fixed
            /// prefix followed by that JSON.
            /// </summary>
            public IEnumerable<string> Reports =>
                Entries.Where(entry => entry.EventId == OrphansObserved)
                    .Select(entry => entry.Message.Substring(entry.Message.IndexOf('{')));

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
                Func<TState, Exception, string> formatter) =>
                Entries.Add((eventId.Id, formatter(state, exception)));

            public bool IsEnabled(LogLevel logLevel) => true;

            public IDisposable BeginScope<TState>(TState state) => throw new NotImplementedException();
        }
    }
}
