using System.Linq;
using System.Threading.Tasks;
using Couchbase.Diagnostics;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Utils;
using Xunit;

namespace Couchbase.IntegrationTests.Diagnostics
{
    public class DiagnosticsReportTests : IClassFixture<ClusterFixture>
    {
        private ICluster _cluster;

        public DiagnosticsReportTests(ClusterFixture fixture)
        {
            _fixture = fixture;
            _cluster = _fixture.Cluster;
            _cluster.BucketAsync("default").GetAwaiter().GetResult();
        }

        private readonly ClusterFixture _fixture;

        [Fact]
        public async Task Can_Create_DiagnosticsReport()
        {
            var report = await _cluster.DiagnosticsAsync();

            Assert.NotNull(report);
            Assert.NotEmpty(report.Id);
            Assert.Equal(ClientIdentifier.GetClientDescription(), report.Sdk);
            Assert.Equal(1, report.Version);

            Assert.Contains(report.Services["kv"], e => e.Type == ServiceType.KeyValue); // at least one KV
            Assert.Contains(report.Services["view"], e => e.Type == ServiceType.Views); // at least one Index
            Assert.Contains(report.Services["n1ql"], e => e.Type == ServiceType.Query); // at least one N1QL

            Assert.Contains(report.Services["fts"], e => e.Type == ServiceType.Search);
            Assert.Contains(report.Services["cbas"], e=>e.Type == ServiceType.Analytics);
        }

        [Fact]
        public async Task Can_Create_DiagnosticsReport_With_ReportId()
        {
            const string reportId = "my-report";
            var report = await _cluster.DiagnosticsAsync(new DiagnosticsOptions().ReportId(reportId));

            Assert.NotNull(report);
            Assert.Equal(reportId, report.Id);
            Assert.Equal(ClientIdentifier.GetClientDescription(), report.Sdk);
            Assert.Equal(1, report.Version);

            Assert.Contains(report.Services["kv"], e => e.Type == ServiceType.KeyValue); // at least one KV
            Assert.Contains(report.Services["view"], e => e.Type == ServiceType.Views); // at least one Index
            Assert.Contains(report.Services["n1ql"], e => e.Type == ServiceType.Query); // at least one N1QL
            Assert.Contains(report.Services["fts"], e => e.Type == ServiceType.Search);
            Assert.Contains(report.Services["cbas"], e => e.Type == ServiceType.Analytics);
        }
    }
}
