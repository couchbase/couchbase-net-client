using System;
using System.Linq;
using Couchbase.Core;
using Couchbase.Core.Monitoring;
using Couchbase.Core.Version;
using Couchbase.IntegrationTests.Utils;
using NUnit.Framework;

namespace Couchbase.IntegrationTests.Core.Monitoring
{
    [TestFixture]
    public class PingReportTests
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
        public void Can_Get_PingReport()
        {
            var report = _bucket.Ping();

            Assert.IsNotNull(report);
            Assert.IsNotEmpty(report.Id);
            Assert.IsTrue(report.Services["kv"].Any(e => e.Type == ServiceType.KeyValue));
            Assert.IsTrue(report.Services["view"].Any(e => e.Type == ServiceType.Views));
            Assert.IsTrue(report.Services["n1ql"].Any(e => e.Type == ServiceType.Query));

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
        public void Can_Get_PingReport_With_Custom_Set_Of_Services()
        {
            var services = new[]
            {
                ServiceType.KeyValue,
                ServiceType.Query
            };

            var report = _bucket.Ping(services);

            Assert.IsNotNull(report);
            Assert.IsNotEmpty(report.Id); // verify report Id has been assigned
            Assert.AreEqual(services.Length, report.Services.Count);

            Assert.IsTrue(report.Services["kv"].Any(e => e.Type == ServiceType.KeyValue)); // at least one KV
            Assert.IsTrue(report.Services["n1ql"].Any(e => e.Type == ServiceType.Query)); // at least one N1QL

            Assert.IsFalse(report.Services.ContainsKey("view"));
            Assert.IsFalse(report.Services.ContainsKey("fts"));
            Assert.IsFalse(report.Services.ContainsKey("cbas"));
        }

        [Test]
        public void Can_Get_PingReport_With_ReportId()
        {
            const string reportId = "report-id";
            var report = _bucket.Ping(reportId);

            Assert.IsNotNull(report);
            Assert.AreEqual(reportId, report.Id);
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
        public void Can_Get_PingReport_With_Custom_Set_Of_Services_And_ReportId()
        {
            const string reportId = "report-id";
            var services = new[]
            {
                ServiceType.KeyValue,
                ServiceType.Query
            };

            var report = _bucket.Ping(reportId, services);

            Assert.IsNotNull(report);
            Assert.AreEqual(reportId, report.Id);
            Assert.AreEqual(services.Length, report.Services.Count);

            Assert.IsTrue(report.Services["kv"].Any(e => e.Type == ServiceType.KeyValue)); // at least one KV
            Assert.IsTrue(report.Services["n1ql"].Any(e => e.Type == ServiceType.Query)); // at least one N1QL

            Assert.IsFalse(report.Services.ContainsKey("view"));
            Assert.IsFalse(report.Services.ContainsKey("fts"));
            Assert.IsFalse(report.Services.ContainsKey("cbas"));
        }
    }
}
