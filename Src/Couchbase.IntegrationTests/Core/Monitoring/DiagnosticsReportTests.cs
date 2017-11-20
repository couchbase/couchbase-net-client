using System;
using System.Linq;
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
        [Test]
        public void Can_Create_DiagnosticsReport()
        {
            using (var cluster = new Cluster(TestConfiguration.GetDefaultConfiguration()))
            {
                cluster.SetupEnhancedAuth();
                cluster.OpenBucket("default");

                var report = cluster.Diagnostics();

                Assert.IsNotNull(report);
                Assert.IsNotEmpty(report.Id);
                Assert.AreEqual(ClientIdentifier.GetClientDescription(), report.Sdk);
                Assert.AreEqual(1, report.Version);

                Assert.IsTrue(report.Services["kv"].Any(e => e.Type == ServiceType.KeyValue)); // at least one KV
                Assert.IsTrue(report.Services["view"].Any(e => e.Type == ServiceType.Views)); // at least one Index
                Assert.IsTrue(report.Services["n1ql"].Any(e => e.Type == ServiceType.Query)); // at least one N1QL

                if (cluster.GetClusterVersion() >= new ClusterVersion(new Version(5, 0)))
                {
                    Assert.IsTrue(report.Services["fts"].Any(e => e.Type == ServiceType.Search));
                }
                if (cluster.GetClusterVersion() >= new ClusterVersion(new Version(6, 0)))
                {
                    Assert.IsFalse(report.Services.ContainsKey("cbas"));
                }
            }
        }

        [Test]
        public void Can_Create_DiagnosticsReport_With_ReportId()
        {
            using (var cluster = new Cluster(TestConfiguration.GetDefaultConfiguration()))
            {
                cluster.SetupEnhancedAuth();
                cluster.OpenBucket("default");

                const string reportId = "my-report";
                var report = cluster.Diagnostics(reportId);

                Assert.IsNotNull(report);
                Assert.AreEqual(reportId, report.Id);
                Assert.AreEqual(ClientIdentifier.GetClientDescription(), report.Sdk);
                Assert.AreEqual(1, report.Version);

                Assert.IsTrue(report.Services["kv"].Any(e => e.Type == ServiceType.KeyValue)); // at least one KV
                Assert.IsTrue(report.Services["view"].Any(e => e.Type == ServiceType.Views)); // at least one Index
                Assert.IsTrue(report.Services["n1ql"].Any(e => e.Type == ServiceType.Query)); // at least one N1QL

                if (cluster.GetClusterVersion() >= new ClusterVersion(new Version(5, 0)))
                {
                    Assert.IsTrue(report.Services["fts"].Any(e => e.Type == ServiceType.Search));
                }
                if (cluster.GetClusterVersion() >= new ClusterVersion(new Version(6, 0)))
                {
                    Assert.IsFalse(report.Services.ContainsKey("cbas"));
                }
            }
        }
    }
}
