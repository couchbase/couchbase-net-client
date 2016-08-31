using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.N1QL;
using NUnit.Framework;

namespace Couchbase.Tests.N1QL
{
    [TestFixture]
    public class QueryResponseTests
    {
        [Test]
        public void Test_Query_Result_Data_Set()
        {
            var resultData = new QueryResultData<dynamic>
            {
                requestID = Guid.NewGuid(),
                clientContextID = "clientContextTestId",
                signature = "testSignature",
                results = new dynamic[] {"{'testresult':'testresultvalue'}"},
                status = QueryStatus.Completed,
                errors = new[] {new ErrorData {code = 1, msg = "testMsg", name = "testName", sev = Severity.None, temp = true}},
                warnings = new[] {new WarningData {code = 2, msg = "testWarningMsg"}},
                metrics =
                    new MetricsData
                    {
                        elapsedTime = "testElapsedTime",
                        errorCount = 0,
                        executionTime = "testExecutionTime",
                        mutationCount = 1,
                        resultCount = 2,
                        resultSize = 3,
                        sortCount = 4,
                        warningCount = 5
                    },
            };

            var result = resultData.ToQueryResult();

            Assert.AreEqual(result.RequestId, resultData.requestID);
            Assert.AreEqual(result.ClientContextId, resultData.clientContextID);
            Assert.AreEqual(result.Signature, resultData.signature);
            Assert.AreEqual(result.Rows.Count, resultData.results.Count());
            Assert.IsTrue(result.Rows.All(r => resultData.results.Any(rd => r == rd)));
            Assert.AreEqual(result.Status, resultData.status);
            Assert.AreEqual(result.Errors.Count, resultData.errors.Count());
            Assert.IsTrue(
                result.Errors.All(
                    e =>
                        resultData.errors.Any(
                            ed =>
                                e.Temp == ed.temp && e.Code == ed.code && e.Message == ed.msg && e.Name == ed.name &&
                                e.Severity == ed.sev)));
            Assert.AreEqual(result.Warnings.Count, resultData.warnings.Count());
            Assert.IsTrue(result.Warnings.All(w => resultData.warnings.Any(wd => w.Code == wd.code && w.Message == wd.msg)));
            Assert.AreEqual(result.Metrics.ElaspedTime, resultData.metrics.elapsedTime);
            Assert.AreEqual(result.Metrics.ErrorCount, resultData.metrics.errorCount);
            Assert.AreEqual(result.Metrics.ExecutionTime, resultData.metrics.executionTime);
            Assert.AreEqual(result.Metrics.MutationCount, resultData.metrics.mutationCount);
            Assert.AreEqual(result.Metrics.ResultCount, resultData.metrics.resultCount);
            Assert.AreEqual(result.Metrics.ResultSize, resultData.metrics.resultSize);
            Assert.AreEqual(result.Metrics.SortCount, resultData.metrics.sortCount);
            Assert.AreEqual(result.Metrics.WarningCount, resultData.metrics.warningCount);
        }
    }
}
