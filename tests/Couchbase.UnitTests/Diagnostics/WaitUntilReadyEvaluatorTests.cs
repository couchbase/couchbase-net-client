using System;
using System.Collections.Generic;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Diagnostics;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Diagnostics
{
    public class WaitUntilReadyEvaluatorTests
    {
        [Theory]
        [InlineData(ServiceType.KeyValue, "kv")]
        [InlineData(ServiceType.Views, "view")]
        [InlineData(ServiceType.Query, "n1ql")]
        [InlineData(ServiceType.Analytics, "cbas")]
        [InlineData(ServiceType.Search, "fts")]
        public void ReportKey_Maps_Pingable_Services(ServiceType serviceType, string expected)
        {
            Assert.Equal(expected, WaitUntilReadyEvaluator.ReportKey(serviceType));
        }

        [Theory]
        [InlineData(ServiceType.Config)]
        [InlineData(ServiceType.Eventing)]
        [InlineData(ServiceType.Management)]
        public void ReportKey_Is_Null_For_Non_Pingable_Services(ServiceType serviceType)
        {
            Assert.Null(WaitUntilReadyEvaluator.ReportKey(serviceType));
        }

        [Fact]
        public void Evaluate_All_Ok_Is_Online_And_Ready()
        {
            //arrange

            var report = CreateReport(
                ("kv", new[] { ServiceState.Ok, ServiceState.Ok }),
                ("n1ql", new[] { ServiceState.Ok }));

            //act

            var result = WaitUntilReadyEvaluator.Evaluate(report,
                new[] { ServiceType.KeyValue, ServiceType.Query }, ClusterState.Online);

            //assert

            Assert.Equal(ClusterState.Online, result.State);
            Assert.True(result.Ready);
        }

        [Fact]
        public void Evaluate_One_Bad_Socket_Is_Degraded()
        {
            //arrange

            var report = CreateReport(
                ("kv", new[] { ServiceState.Ok, ServiceState.Error }),
                ("n1ql", new[] { ServiceState.Ok }));
            var expected = new[] { ServiceType.KeyValue, ServiceType.Query };

            //act

            var wantsOnline = WaitUntilReadyEvaluator.Evaluate(report, expected, ClusterState.Online);
            var wantsDegraded = WaitUntilReadyEvaluator.Evaluate(report, expected, ClusterState.Degraded);

            //assert

            Assert.Equal(ClusterState.Degraded, wantsOnline.State);
            Assert.False(wantsOnline.Ready);
            Assert.True(wantsDegraded.Ready);
        }

        [Fact]
        public void Evaluate_Online_Also_Satisfies_A_Desired_Degraded()
        {
            //arrange

            var report = CreateReport(("kv", new[] { ServiceState.Ok }));

            //act

            var result = WaitUntilReadyEvaluator.Evaluate(report, new[] { ServiceType.KeyValue }, ClusterState.Degraded);

            //assert

            Assert.Equal(ClusterState.Online, result.State);
            Assert.True(result.Ready);
        }

        [Fact]
        public void Evaluate_Expected_Service_Absent_From_Report_Is_Not_Ready()
        {
            //arrange

            var report = CreateReport(("kv", new[] { ServiceState.Ok }));
            var expected = new[] { ServiceType.KeyValue, ServiceType.Query };

            //act

            var wantsOnline = WaitUntilReadyEvaluator.Evaluate(report, expected, ClusterState.Online);
            var wantsDegraded = WaitUntilReadyEvaluator.Evaluate(report, expected, ClusterState.Degraded);

            //assert

            Assert.Equal(ClusterState.Offline, wantsOnline.State);
            Assert.False(wantsOnline.Ready);
            Assert.False(wantsDegraded.Ready);
        }

        [Fact]
        public void Evaluate_Expected_Service_With_Empty_List_Is_Not_Ready()
        {
            //arrange

            var report = CreateReport(
                ("kv", new ServiceState[0]),
                ("n1ql", new[] { ServiceState.Ok }));
            var expected = new[] { ServiceType.KeyValue, ServiceType.Query };

            //act

            var wantsOnline = WaitUntilReadyEvaluator.Evaluate(report, expected, ClusterState.Online);
            var wantsDegraded = WaitUntilReadyEvaluator.Evaluate(report, expected, ClusterState.Degraded);

            //assert

            Assert.Equal(ClusterState.Offline, wantsOnline.State);
            Assert.False(wantsOnline.Ready);
            Assert.False(wantsDegraded.Ready);
        }

        [Fact]
        public void Evaluate_No_Socket_Ok_For_A_Service_Is_Offline()
        {
            //arrange

            var report = CreateReport(
                ("kv", new[] { ServiceState.Ok }),
                ("n1ql", new[] { ServiceState.Error, ServiceState.Error }));

            //act

            var result = WaitUntilReadyEvaluator.Evaluate(report,
                new[] { ServiceType.KeyValue, ServiceType.Query }, ClusterState.Degraded);

            //assert

            Assert.Equal(ClusterState.Offline, result.State);
            Assert.False(result.Ready);
        }

        [Fact]
        public void Evaluate_Empty_Expected_Set_Is_Ready()
        {
            //arrange

            var report = CreateReport();

            //act

            var result = WaitUntilReadyEvaluator.Evaluate(report, new ServiceType[0], ClusterState.Online);

            //assert

            Assert.Equal(ClusterState.Online, result.State);
            Assert.True(result.Ready);
        }

        [Fact]
        public void Evaluate_Ignores_Services_That_Are_Not_Expected()
        {
            //arrange

            var report = CreateReport(
                ("kv", new[] { ServiceState.Ok }),
                ("cbas", new[] { ServiceState.Error }));

            //act

            var result = WaitUntilReadyEvaluator.Evaluate(report, new[] { ServiceType.KeyValue }, ClusterState.Online);

            //assert

            Assert.Equal(ClusterState.Online, result.State);
            Assert.True(result.Ready);
        }

        [Fact]
        public void Describe_Reports_Missing_And_Partial_Services()
        {
            //arrange

            var report = CreateReport(("kv", new[] { ServiceState.Ok, ServiceState.Error }));

            //act

            var description = WaitUntilReadyEvaluator.Describe(report,
                new[] { ServiceType.KeyValue, ServiceType.Query });

            //assert

            Assert.Equal("kv 1/2 ok, n1ql missing", description);
        }

        [Fact]
        public void Backoff_Doubles_From_Ten_Milliseconds()
        {
            //arrange

            var backoff = new WaitUntilReadyBackoff();

            //act

            var delays = new List<double>();
            for (var i = 0; i < 5; i++)
            {
                delays.Add(backoff.Next().TotalMilliseconds);
            }

            //assert

            Assert.Equal(new double[] { 10, 20, 40, 80, 160 }, delays);
        }

        [Fact]
        public void Backoff_Caps_At_One_Second()
        {
            //arrange

            var backoff = new WaitUntilReadyBackoff();

            //act

            TimeSpan delay = default;
            for (var i = 0; i < 20; i++)
            {
                delay = backoff.Next();
            }

            //assert

            Assert.Equal(TimeSpan.FromSeconds(1), delay);
        }

        [Fact]
        public void ExpectedServices_Includes_The_Http_Services_Without_Their_Clients()
        {
            //arrange

            var config = CreateConfig();

            //act

            var expected = WaitUntilReadyEvaluator.ExpectedServices(null, config,
                new[] { ServiceType.Query, ServiceType.Search, ServiceType.Analytics }, bucketLevel: true);

            //assert

            Assert.Equal(new HashSet<ServiceType>
            {
                ServiceType.Query, ServiceType.Search, ServiceType.Analytics
            }, expected);
        }

        [Fact]
        public void ExpectedServices_Drops_Views_Without_A_View_Client()
        {
            //arrange

            var config = CreateConfig();

            //act

            var expected = WaitUntilReadyEvaluator.ExpectedServices(null, config,
                new[] { ServiceType.Views }, bucketLevel: true);

            //assert

            Assert.Empty(expected);
        }

        [Fact]
        public void HasTopology_Is_False_Without_A_Config_And_Without_Nodes()
        {
            Assert.False(WaitUntilReadyEvaluator.HasTopology(null, Array.Empty<IClusterNode>()));
            Assert.False(WaitUntilReadyEvaluator.HasTopology(null, null));
        }

        [Fact]
        public void HasTopology_Is_True_With_A_Config()
        {
            Assert.True(WaitUntilReadyEvaluator.HasTopology(CreateConfig(), Array.Empty<IClusterNode>()));
        }

        [Fact]
        public void HasTopology_Is_True_With_A_Node()
        {
            Assert.True(WaitUntilReadyEvaluator.HasTopology(null, new[] { new Mock<IClusterNode>().Object }));
        }

        private static BucketConfig CreateConfig() =>
            new()
            {
                Name = "default",
                BucketCapabilities = new List<string> { BucketCapabilities.COUCHAPI },
                NodesExt = new List<NodesExt>
                {
                    new()
                    {
                        Hostname = "node1",
                        Services = new Services
                        {
                            Kv = 11210,
                            Capi = 8092,
                            N1Ql = 8093,
                            Fts = 8094,
                            Cbas = 8095
                        }
                    }
                }
            };

        private static IPingReport CreateReport(params (string Key, ServiceState[] States)[] services)
        {
            var endpoints = new Dictionary<string, IEnumerable<IEndpointDiagnostics>>();
            foreach (var service in services)
            {
                var entries = new List<IEndpointDiagnostics>();
                foreach (var state in service.States)
                {
                    entries.Add(new EndpointDiagnostics { State = state });
                }

                endpoints.Add(service.Key, entries);
            }

            return new PingReport("report-id", 0, endpoints);
        }
    }
}
