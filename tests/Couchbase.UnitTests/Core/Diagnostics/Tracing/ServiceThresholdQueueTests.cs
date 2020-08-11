using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics.Tracing;
using Xunit;

namespace Couchbase.UnitTests.Core.Diagnostics.Tracing
{
    public class ServiceThresholdQueueTests
    {
        [Fact]
        public async Task Add_Doesnt_Throw_While_Reporting()
        {
            var cts = new CancellationTokenSource();
            var addTasks = from i in Enumerable.Range(0, 100)
                select Task.Run(async () =>
                {
                    while (!cts.IsCancellationRequested)
                    {
                        ulong operationId = 0;
                        ServiceThresholdQueue.AddByService(RequestTracing.ServiceIdentifier.Kv, new ThresholdSummary(
                            operationName: "operation_" + i,
                            lastOperationId: (operationId++).ToString(),
                            lastLocalAddress: Dns.GetHostName(),
                            lastRemoteAddress: "unit_test_host:443",
                            lastLocalId: null,
                            totalUs: 100,
                            encodeUs: 80,
                            serverUs: 5,
                            dispatchUs: 18,
                            lastDispatchUs: 18
                            ));

                        await Task.Delay(1, cts.Token);
                    }
                });

            for (int i = 0; i < 100; i++)
            {
                _ = ServiceThresholdQueue.ReportSummaries();
            }

            cts.Cancel(false);

            await Task.WhenAll(addTasks);
        }

        [Fact]
        public void Report_Complies_With_Item_Limit()
        {
            ServiceThresholdQueue.SetSampleSize(9);
            for (int i = 0; i < 100; i++)
            {
                ulong operationId = 0;
                ServiceThresholdQueue.AddByService(RequestTracing.ServiceIdentifier.Kv, new ThresholdSummary(
                    operationName: "operation_" + i,
                    lastOperationId: (operationId++).ToString(),
                    lastLocalAddress: Dns.GetHostName(),
                    lastRemoteAddress: "unit_test_host:443",
                    lastLocalId: null,
                    totalUs: 100,
                    encodeUs: 80,
                    serverUs: 5,
                    dispatchUs: 18,
                    lastDispatchUs: 18
                ));
            }

            var reports = ServiceThresholdQueue.ReportSummaries().ToList();
            Assert.NotNull(reports);
            Assert.Single(reports);
            Assert.Equal(9, reports[0].top.Length);
        }
    }
}
