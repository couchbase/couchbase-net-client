using System;
using System.Linq;
using Couchbase.Core.Monitoring;
using Couchbase.Core.Version;
using Couchbase.IntegrationTests.Utils;
using NUnit.Framework;

namespace Couchbase.IntegrationTests.Core.Monitoring
{
    [TestFixture]
    public class PingReportTests
    {
        [Test]
        public void Can_Get_PingReport()
        {
            using (var cluster = new Cluster(TestConfiguration.GetDefaultConfiguration()))
            {
                cluster.SetupEnhancedAuth();
                var bucket = cluster.OpenBucket("default");

                var report = bucket.Ping();

                Assert.IsNotNull(report);
                Assert.IsNotEmpty(report.Id);
                Assert.IsTrue(report.Services["kv"].Any(e => e.Type == ServiceType.KeyValue));
                Assert.IsTrue(report.Services["view"].Any(e => e.Type == ServiceType.Views));
                Assert.IsTrue(report.Services["n1ql"].Any(e => e.Type == ServiceType.Query));

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
        public void Can_Get_PingReport_With_Custom_Set_Of_Services()
        {
            using (var cluster = new Cluster(TestConfiguration.GetDefaultConfiguration()))
            {
                cluster.SetupEnhancedAuth();
                var bucket = cluster.OpenBucket("default");

                var services = new[]
                {
                    ServiceType.KeyValue,
                    ServiceType.Query
                };

                var report = bucket.Ping(services);

                Assert.IsNotNull(report);
                Assert.IsNotEmpty(report.Id); // verify report Id has been assigned
                Assert.AreEqual(services.Length, report.Services.Count);

                Assert.IsTrue(report.Services["kv"].Any(e => e.Type == ServiceType.KeyValue)); // at least one KV
                Assert.IsTrue(report.Services["n1ql"].Any(e => e.Type == ServiceType.Query)); // at least one N1QL

                Assert.IsFalse(report.Services.ContainsKey("view"));
                Assert.IsFalse(report.Services.ContainsKey("fts"));
                Assert.IsFalse(report.Services.ContainsKey("cbas"));
            }
        }

        [Test]
        public void Can_Get_PingReport_With_ReportId()
        {
            using (var cluster = new Cluster(TestConfiguration.GetDefaultConfiguration()))
            {
                cluster.SetupEnhancedAuth();
                var bucket = cluster.OpenBucket("default");

                const string reportId = "report-id";
                var report = bucket.Ping(reportId);

                Assert.IsNotNull(report);
                Assert.AreEqual(reportId, report.Id);
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
        public void Can_Get_PingReport_With_Custom_Set_Of_Services_And_ReportId()
        {
            using (var cluster = new Cluster(TestConfiguration.GetDefaultConfiguration()))
            {
                cluster.SetupEnhancedAuth();
                var bucket = cluster.OpenBucket("default");

                const string reportId = "report-id";
                var services = new[]
                {
                    ServiceType.KeyValue,
                    ServiceType.Query
                };

                var report = bucket.Ping(reportId, services);

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
}
