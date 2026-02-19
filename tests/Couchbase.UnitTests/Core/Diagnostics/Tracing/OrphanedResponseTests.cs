using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Diagnostics.Tracing.OrphanResponseReporting;
using Couchbase.UnitTests.Core.Diagnostics.Metrics;
using Couchbase.UnitTests.Helpers;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.UnitTests.Core.Diagnostics.Tracing
{
    public class OrphanedResponseTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public OrphanedResponseTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public async Task Test()
        {
            var loggerFactory = new LoggingMeterTests.LoggingMeterTestFactory();
            var orphanReporter = new OrphanReporter(loggerFactory.CreateLogger<OrphanReporter>(), new OrphanOptions{EmitInterval = TimeSpan.FromSeconds(1)});
            orphanReporter.Add(GetOrphanSummary(OuterRequestSpans.ServiceSpan.Kv.Name));
            orphanReporter.Add(GetOrphanSummary(OuterRequestSpans.ServiceSpan.N1QLQuery));
            orphanReporter.Add(GetOrphanSummary(OuterRequestSpans.ServiceSpan.Kv.Name));
            orphanReporter.Add(GetOrphanSummary(OuterRequestSpans.ServiceSpan.N1QLQuery));

            string report = null;
            // Use async polling instead of SpinWait to avoid starving background tasks on overloaded CI
            var finished = await AsyncTestHelper.WaitForConditionAsync(
                () => loggerFactory.LoggedData.TryTake(out report),
                timeout: TimeSpan.FromSeconds(30));
            Assert.True(finished, userMessage: "Did not find a log entry for orphaned data.");
            Assert.NotNull(report);

            _testOutputHelper.WriteLine(report);
        }

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
    }
}
