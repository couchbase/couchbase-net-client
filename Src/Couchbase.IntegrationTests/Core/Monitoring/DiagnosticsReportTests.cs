using System;
using System.Linq;
using Couchbase.Core;
using Couchbase.Core.Monitoring;
using Couchbase.Core.Version;
using Couchbase.IntegrationTests.Utils;
using Couchbase.Utils;
using NUnit.Framework;

namespace Couchbase.IntegrationTests.Core.Monitoring
{
    [TestFixture]
    public class DiagnosticsReportTests
    {
        private ICluster _cluster;
        private IBucket _bucket;

        [OneTimeSetUp]
        public void Setup()
        {
            _cluster = new Cluster(TestConfiguration.GetDefaultConfiguration());
            _cluster.SetupEnhancedAuth();
            _bucket = _cluster.OpenBucket("default");
        }

        [Test]
        public void Can_Create_DiagnosticsReport()
        {
            var report = _cluster.Diagnostics();

            Assert.IsNotNull(report);
            Assert.IsNotEmpty(report.Id);
            Assert.AreEqual(ClientIdentifier.GetClientDescription(), report.Sdk);
            Assert.AreEqual(1, report.Version);

            Assert.IsTrue(report.Services["kv"].Any(e => e.Type == ServiceType.KeyValue)); // at least one KV
            Assert.IsTrue(report.Services["view"].Any(e => e.Type == ServiceType.Views)); // at least one Index
            Assert.IsTrue(report.Services["n1ql"].Any(e => e.Type == ServiceType.Query)); // at least one N1QL

            if (_cluster.GetClusterVersion() >= new ClusterVersion(new Version(5, 0)))
            {
                Assert.IsTrue(report.Services["fts"].Any(e => e.Type == ServiceType.Search));
            }
            if (_cluster.GetClusterVersion() >= new ClusterVersion(new Version(6, 0)))
            {
                Assert.IsFalse(report.Services.ContainsKey("cbas"));
            }
        }

        [Test]
        public void Can_Create_DiagnosticsReport_With_ReportId()
        {
            const string reportId = "my-report";
            var report = _cluster.Diagnostics(reportId);

            Assert.IsNotNull(report);
            Assert.AreEqual(reportId, report.Id);
            Assert.AreEqual(ClientIdentifier.GetClientDescription(), report.Sdk);
            Assert.AreEqual(1, report.Version);

            Assert.IsTrue(report.Services["kv"].Any(e => e.Type == ServiceType.KeyValue)); // at least one KV
            Assert.IsTrue(report.Services["view"].Any(e => e.Type == ServiceType.Views)); // at least one Index
            Assert.IsTrue(report.Services["n1ql"].Any(e => e.Type == ServiceType.Query)); // at least one N1QL

            if (_cluster.GetClusterVersion() >= new ClusterVersion(new Version(5, 0)))
            {
                Assert.IsTrue(report.Services["fts"].Any(e => e.Type == ServiceType.Search));
            }
            if (_cluster.GetClusterVersion() >= new ClusterVersion(new Version(6, 0)))
            {
                Assert.IsFalse(report.Services.ContainsKey("cbas"));
            }
        }
    }
}
