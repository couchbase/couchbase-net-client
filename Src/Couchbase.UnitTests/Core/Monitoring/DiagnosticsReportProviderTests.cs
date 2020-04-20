using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Couchbase.Configuration;
using Couchbase.Core;
using Couchbase.Core.Monitoring;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.N1QL;
using Couchbase.Utils;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.Core.Monitoring
{
    [TestFixture]
    public class DiagnosticsReportTests
    {
        [Test]
        public void Can_Create_DiagnosticsReport()
        {
            var reportId = Guid.NewGuid().ToString();
            var kvLastActivity = DateTime.UtcNow;
            var viewLastActivity = kvLastActivity.AddSeconds(-5);
            var queryLastActivity = kvLastActivity.AddSeconds(-10);
            var searchLastActivity = kvLastActivity.AddSeconds(-15);
            var analyticsLastActivity = kvLastActivity.AddSeconds(-20);

            var connection = Mock.Of<IConnection>(c =>
                c.LastActivity == kvLastActivity &&
                c.IsConnected == true &&
                c.IsAuthenticated == true
            );

            var connectionPool = Mock.Of<IConnectionPool>(p =>
                p.Connections == new List<IConnection> {connection}
            );

            var server = Mock.Of<IServer>(s =>
                s.IsDataNode == true &&
                s.ConnectionPool == connectionPool &&
                s.IsViewNode == true &&
                s.ViewClient.LastActivity == viewLastActivity &&
                s.IsQueryNode == true &&
                s.QueryClient.LastActivity == queryLastActivity &&
                s.IsSearchNode == true &&
                s.SearchClient.LastActivity == searchLastActivity &&
                s.IsAnalyticsNode == true &&
                s.AnalyticsClient.LastActivity == analyticsLastActivity
            );

            var configInfo = Mock.Of<IConfigInfo>(c =>
                c.Servers == new List<IServer> {server}
            );

            var report = DiagnosticsReportProvider.CreateDiagnosticsReport(reportId, new[] {configInfo});
            Assert.IsNotNull(report);
            Assert.AreEqual(reportId, report.Id);
            Assert.AreEqual(1, report.Version);
            Assert.AreEqual(ClientIdentifier.GetClientDescription(), report.Sdk);

            Assert.IsTrue(report.Services["kv"].First(x => x.Type == ServiceType.KeyValue).LastActivity > 0);
            Assert.IsTrue(report.Services["view"].First(x => x.Type == ServiceType.Views).LastActivity > 0);
            Assert.IsTrue(report.Services["n1ql"].First(x => x.Type == ServiceType.Query).LastActivity > 0);
            Assert.IsTrue(report.Services["fts"].First(x => x.Type == ServiceType.Search).LastActivity > 0);
            Assert.IsTrue(report.Services["cbas"].First(x => x.Type == ServiceType.Analytics).LastActivity > 0);
        }

        [Test]
        public void Can_Create_Ping_Report_Failed()
        {
            var reportId = Guid.NewGuid().ToString();

            var kvLastActivity = DateTime.UtcNow;
            var viewLastActivity = kvLastActivity.AddSeconds(-5);
            var queryLastActivity = kvLastActivity.AddSeconds(-10);
            var searchLastActivity = kvLastActivity.AddSeconds(-15);
            var analyticsLastActivity = kvLastActivity.AddSeconds(-20);

            var connection = Mock.Of<IConnection>(c =>
                c.LastActivity == kvLastActivity &&
                c.IsConnected == true &&
                c.IsAuthenticated == true
            );

            var connectionPool = Mock.Of<IConnectionPool>(p =>
                p.Connections == new List<IConnection> { connection }
            );

            var server = Mock.Of<IServer>(s =>
                s.IsDataNode == true &&
                s.ConnectionPool == connectionPool &&
                s.IsViewNode == true &&
                s.ViewClient.LastActivity == viewLastActivity &&
                s.IsQueryNode == true &&
                s.QueryClient.LastActivity == queryLastActivity &&
                s.IsSearchNode == true &&
                s.SearchClient.LastActivity == searchLastActivity &&
                s.IsAnalyticsNode == true &&
                s.AnalyticsClient.LastActivity == analyticsLastActivity
            );

            Mock.Get(connection).Setup(x=>x.Send(It.IsAny<byte[]>())).Throws<SocketException>();

            Mock.Get(server).Setup(x => x.QueryClient.Query<dynamic>(It.IsAny<IQueryRequest>()))
                .Returns(new QueryResult<dynamic>
                {
                    Exception = new Exception()
                });

            var configInfo = Mock.Of<IConfigInfo>(c =>
                c.Servers == new List<IServer> { server }
            );

            var report = DiagnosticsReportProvider.CreatePingReport(reportId, configInfo, new[] { ServiceType.KeyValue, ServiceType.Query});
            Assert.IsNotNull(report);

            foreach (var endpointDiagnostic in report.Services.Values.SelectMany(service => service))
            {
                Assert.AreEqual(ServiceState.Error, endpointDiagnostic.State);
            }

            Assert.AreEqual(reportId, report.Id);
            Assert.AreEqual(1, report.Version);
            Assert.AreEqual(ClientIdentifier.GetClientDescription(), report.Sdk);
        }


        [Test]
        public void Can_Create_Ping_Report_Success()
        {
            var reportId = Guid.NewGuid().ToString();

            var kvLastActivity = DateTime.UtcNow;
            var viewLastActivity = kvLastActivity.AddSeconds(-5);
            var queryLastActivity = kvLastActivity.AddSeconds(-10);
            var searchLastActivity = kvLastActivity.AddSeconds(-15);
            var analyticsLastActivity = kvLastActivity.AddSeconds(-20);

            var connection = Mock.Of<IConnection>(c =>
                c.LastActivity == kvLastActivity &&
                c.IsConnected == true &&
                c.IsAuthenticated == true
            );

            var connectionPool = Mock.Of<IConnectionPool>(p =>
                p.Connections == new List<IConnection> { connection }
            );

            var server = Mock.Of<IServer>(s =>
                s.IsDataNode == true &&
                s.ConnectionPool == connectionPool &&
                s.IsViewNode == true &&
                s.ViewClient.LastActivity == viewLastActivity &&
                s.IsQueryNode == true &&
                s.QueryClient.LastActivity == queryLastActivity &&
                s.IsSearchNode == true &&
                s.SearchClient.LastActivity == searchLastActivity &&
                s.IsAnalyticsNode == true &&
                s.AnalyticsClient.LastActivity == analyticsLastActivity
            );

            //just assume nothing is thrown, however, server could return back a status
            Mock.Get(connection).Setup(x => x.Send(It.IsAny<byte[]>())).Returns(new byte[] {0x00});

            Mock.Get(server).Setup(x => x.QueryClient.Query<dynamic>(It.IsAny<IQueryRequest>()))
                .Returns(new QueryResult<dynamic>
                {
                    Success = true,
                    HttpStatusCode = HttpStatusCode.OK
                });

            var configInfo = Mock.Of<IConfigInfo>(c =>
                c.Servers == new List<IServer> { server }
            );

            var report = DiagnosticsReportProvider.CreatePingReport(reportId, configInfo, new[] { ServiceType.KeyValue, ServiceType.Query });
            Assert.IsNotNull(report);

            foreach (var endpointDiagnostic in report.Services.Values.SelectMany(service => service))
            {
                Assert.AreEqual(ServiceState.Ok, endpointDiagnostic.State);
            }
            Assert.AreEqual(reportId, report.Id);
            Assert.AreEqual(1, report.Version);
            Assert.AreEqual(ClientIdentifier.GetClientDescription(), report.Sdk);
        }

        [Test]
        public void Verify_DiagnosticsReport_Json_Output()
        {
            var endpoints = new Dictionary<string, IEnumerable<IEndpointDiagnostics>>
            {
                {
                    "fts", new List<EndpointDiagnostics>
                    {
                        new EndpointDiagnostics
                        {
                            Id = "0x1415F11",
                            LastActivity = 1182000,
                            Local = "127.0.0.1:54669",
                            Remote = "centos7-lx1.home.somewhere.org:8094",
                            State = ServiceState.Connected
                        }
                    }
                },
                {
                    "kv", new List<EndpointDiagnostics>
                    {
                        new EndpointDiagnostics
                        {
                            Id = "0x1415F12",
                            LastActivity = 1182000,
                            Local = "127.0.0.1:54670",
                            Remote = "centos7-lx1.home.somewhere.org:11210",
                            State = ServiceState.Connected,
                            Scope = "bucketname"
                        }
                    }
                },
                {
                    "n1ql", new List<EndpointDiagnostics>
                    {
                        new EndpointDiagnostics
                        {
                            Id = "0x1415F13",
                            LastActivity = 1182000,
                            Local = "127.0.0.1:54671",
                            Remote = "centos7-lx1.home.somewhere.org:8093",
                            State = ServiceState.Connected
                        },
                        new EndpointDiagnostics
                        {
                            Id = "0x1415F14",
                            LastActivity = 1182000,
                            Local = "127.0.0.1:54682",
                            Remote = "centos7-lx2.home.somewhere.org:8095",
                            State = ServiceState.Disconnected
                        }
                    }
                },
                {
                    "cbas", new List<EndpointDiagnostics>
                    {
                        new EndpointDiagnostics
                        {
                            Id = "0x1415F15",
                            LastActivity = 1182000,
                            Local = "127.0.0.1:54675",
                            Remote = "centos7-lx1.home.somewhere.org:8095",
                            State = ServiceState.Connected
                        }
                    }
                },
                {
                    "view", new List<EndpointDiagnostics>
                    {
                        new EndpointDiagnostics
                        {
                            Id = "0x1415F16",
                            LastActivity = 1182000,
                            Local = "127.0.0.1:54672",
                            Remote = "centos7-lx1.home.somewhere.org:8092",
                            State = ServiceState.Connected
                        }
                    }
                }
            };

            var report = new DiagnosticsReport("0xdeadbeef", endpoints);
            var expected = JsonConvert.SerializeObject(
                JsonConvert.DeserializeObject(ResourceHelper.ReadResource(@"Data\diagnostics_report.json")),
                Formatting.None
            ).Replace("[[sdk_version]]", ClientIdentifier.GetClientDescription());

            Assert.AreEqual(expected, report.ToString());
        }

        [Test]
        public void Verify_PingReport_Json_Output()
        {
            var endpoints = new Dictionary<string, IEnumerable<IEndpointDiagnostics>>
            {
                {
                    "fts", new List<EndpointDiagnostics>
                    {
                        new EndpointDiagnostics
                        {
                            Id = "0x1415F11",
                            Latency = 877909,
                            Local = "127.0.0.1:54669",
                            Remote = "centos7-lx1.home.somewhere.org:8094",
                            State = ServiceState.Ok
                        }
                    }
                },
                {
                    "kv", new List<EndpointDiagnostics>
                    {
                        new EndpointDiagnostics
                        {
                            Id = "0x1415F12",
                            Latency = 1182000,
                            Local = "127.0.0.1:54670",
                            Remote = "centos7-lx1.home.somewhere.org:11210",
                            State = ServiceState.Ok,
                            Scope = "bucketname"
                        }
                    }
                },
                {
                    "n1ql", new List<EndpointDiagnostics>
                    {
                        new EndpointDiagnostics
                        {
                            Id = "0x1415F13",
                            Latency = 6217,
                            Local = "127.0.0.1:54671",
                            Remote = "centos7-lx1.home.somewhere.org:8093",
                            State = ServiceState.Ok
                        },
                        new EndpointDiagnostics
                        {
                            Id = "0x1415F14",
                            Latency = 2213,
                            Local = "127.0.0.1:54682",
                            Remote = "centos7-lx2.home.somewhere.org:8095",
                            State = ServiceState.Timeout
                        }
                    }
                },
                {
                    "cbas", new List<EndpointDiagnostics>
                    {
                        new EndpointDiagnostics
                        {
                            Id = "0x1415F15",
                            Latency = 2213,
                            Local = "127.0.0.1:54675",
                            Remote = "centos7-lx1.home.somewhere.org:8095",
                            State = ServiceState.Error
                        }
                    }
                },
                {
                    "view", new List<EndpointDiagnostics>
                    {
                        new EndpointDiagnostics
                        {
                            Id = "0x1415F16",
                            Latency = 45585,
                            Local = "127.0.0.1:54672",
                            Remote = "centos7-lx1.home.somewhere.org:8092",
                            State = ServiceState.Ok
                        }
                    }
                }
            };

            var report = new PingReport("0xdeadbeef", 53, endpoints);
            var expected = JsonConvert.SerializeObject(
                JsonConvert.DeserializeObject(ResourceHelper.ReadResource(@"Data\ping_report.json")),
                Formatting.None
            ).Replace("[[sdk_version]]", ClientIdentifier.GetClientDescription());

            Assert.AreEqual(expected, report.ToString());
        }
    }
};
