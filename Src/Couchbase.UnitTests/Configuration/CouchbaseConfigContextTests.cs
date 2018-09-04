using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.Utils;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.Configuration
{
    [TestFixture]
    public class CouchbaseConfigContextTests
    {
        [Test]
        public void When_Loading_Config_EnhancedDurability_Is_True_If_IOService_Indicates_It_Is_Enabled()
        {
            var mockBucketConfig = new Mock<IBucketConfig>();
            mockBucketConfig.Setup(x => x.Name).Returns("default");
            mockBucketConfig.Setup(x => x.Nodes).Returns(new [] { new Node { Hostname = "127.0.0.1"} });
            mockBucketConfig.Setup(x => x.VBucketServerMap).Returns(new VBucketServerMap());

            var mockConnectionPool = new Mock<IConnectionPool>();
            var mockIoService = new Mock<IIOService>();
            mockIoService.Setup(x => x.ConnectionPool).Returns(mockConnectionPool.Object);
            mockIoService.Setup(x => x.SupportsEnhancedDurability).Returns(true);
            var mockSasl = new Mock<ISaslMechanism>();

            var clientConfig = new ClientConfiguration();
            var context = new CouchbaseConfigContext(
                mockBucketConfig.Object,
                clientConfig,
                p => mockIoService.Object,
                (a, b) => mockConnectionPool.Object,
                (a, b, c, d) => mockSasl.Object,
                new DefaultTranscoder(),
                null, null);

            context.LoadConfig();

            Assert.IsTrue(context.SupportsEnhancedDurability);
        }

        [Test]
        public void When_Loading_Config_With_BucketConfig_EnhancedDurability_Is_True_If_IOService_Indicates_It_Is_Enabled()
        {
            var mockBucketConfig = new Mock<IBucketConfig>();
            mockBucketConfig.Setup(x => x.Name).Returns("default");
            mockBucketConfig.Setup(x => x.Nodes).Returns(new[] { new Node { Hostname = "127.0.0.1" } });
            mockBucketConfig.Setup(x => x.VBucketServerMap).Returns(new VBucketServerMap());

            var mockConnectionPool = new Mock<IConnectionPool>();
            var mockIoService = new Mock<IIOService>();
            mockIoService.Setup(x => x.ConnectionPool).Returns(mockConnectionPool.Object);
            mockIoService.Setup(x => x.SupportsEnhancedDurability).Returns(true);
            var mockSasl = new Mock<ISaslMechanism>();

            var clientConfig = new ClientConfiguration();
            var context = new CouchbaseConfigContext(
                mockBucketConfig.Object,
                clientConfig,
                p => mockIoService.Object,
                (a, b) => mockConnectionPool.Object,
                (a, b, c, d) => mockSasl.Object,
                new DefaultTranscoder(),
                null, null);

            context.LoadConfig(mockBucketConfig.Object);

            Assert.IsTrue(context.SupportsEnhancedDurability);
        }

        [Test]
        public void When_Loading_Config_With_IOService_EnhancedDurability_Is_True_If_IOService_Indicates_It_Is_Enabled()
        {
            var mockBucketConfig = new Mock<IBucketConfig>();
            mockBucketConfig.Setup(x => x.Name).Returns("default");
            mockBucketConfig.Setup(x => x.Nodes).Returns(new[] { new Node { Hostname = "127.0.0.1" } });
            mockBucketConfig.Setup(x => x.VBucketServerMap).Returns(new VBucketServerMap());

            var mockConnectionPool = new Mock<IConnectionPool>();
            var mockIoService = new Mock<IIOService>();
            mockIoService.Setup(x => x.ConnectionPool).Returns(mockConnectionPool.Object);
            mockIoService.Setup(x => x.SupportsEnhancedDurability).Returns(true);
            var mockSasl = new Mock<ISaslMechanism>();

            var clientConfig = new ClientConfiguration();
            var context = new CouchbaseConfigContext(
                mockBucketConfig.Object,
                clientConfig,
                p => mockIoService.Object,
                (a, b) => mockConnectionPool.Object,
                (a, b, c, d) => mockSasl.Object,
                new DefaultTranscoder(),
                null, null);

            context.LoadConfig(mockIoService.Object);

            Assert.IsTrue(context.SupportsEnhancedDurability);
        }

        [Test]
        public void When_Loading_Config_SubdocXAttributes_Is_True_If_IOService_Indicates_It_Is_Enabled()
        {
            var mockBucketConfig = new Mock<IBucketConfig>();
            mockBucketConfig.Setup(x => x.Name).Returns("default");
            mockBucketConfig.Setup(x => x.Nodes).Returns(new[] { new Node { Hostname = "127.0.0.1" } });
            mockBucketConfig.Setup(x => x.VBucketServerMap).Returns(new VBucketServerMap());

            var mockConnectionPool = new Mock<IConnectionPool>();
            var mockIoService = new Mock<IIOService>();
            mockIoService.Setup(x => x.ConnectionPool).Returns(mockConnectionPool.Object);
            mockIoService.Setup(x => x.SupportsSubdocXAttributes).Returns(true);
            var mockSasl = new Mock<ISaslMechanism>();

            var clientConfig = new ClientConfiguration();
            var context = new CouchbaseConfigContext(
                mockBucketConfig.Object,
                clientConfig,
                p => mockIoService.Object,
                (a, b) => mockConnectionPool.Object,
                (a, b, c, d) => mockSasl.Object,
                new DefaultTranscoder(),
                null, null);

            context.LoadConfig();

            Assert.IsTrue(context.SupportsSubdocXAttributes);
        }

        [Test]
        public void When_Loading_Config_With_BucketConfig_SubdocXAttributes_Is_True_If_IOService_Indicates_It_Is_Enabled()
        {
            var mockBucketConfig = new Mock<IBucketConfig>();
            mockBucketConfig.Setup(x => x.Name).Returns("default");
            mockBucketConfig.Setup(x => x.Nodes).Returns(new[] { new Node { Hostname = "127.0.0.1" } });
            mockBucketConfig.Setup(x => x.VBucketServerMap).Returns(new VBucketServerMap());

            var mockConnectionPool = new Mock<IConnectionPool>();
            var mockIoService = new Mock<IIOService>();
            mockIoService.Setup(x => x.ConnectionPool).Returns(mockConnectionPool.Object);
            mockIoService.Setup(x => x.SupportsSubdocXAttributes).Returns(true);
            var mockSasl = new Mock<ISaslMechanism>();

            var clientConfig = new ClientConfiguration();
            var context = new CouchbaseConfigContext(
                mockBucketConfig.Object,
                clientConfig,
                p => mockIoService.Object,
                (a, b) => mockConnectionPool.Object,
                (a, b, c, d) => mockSasl.Object,
                new DefaultTranscoder(),
                null, null);

            context.LoadConfig(mockBucketConfig.Object);

            Assert.IsTrue(context.SupportsSubdocXAttributes);
        }

        [Test]
        public void When_Loading_Config_With_IOService_SubdocXAttributes_Is_True_If_IOService_Indicates_It_Is_Enabled()
        {
            var mockBucketConfig = new Mock<IBucketConfig>();
            mockBucketConfig.Setup(x => x.Name).Returns("default");
            mockBucketConfig.Setup(x => x.Nodes).Returns(new[] { new Node { Hostname = "127.0.0.1" } });
            mockBucketConfig.Setup(x => x.VBucketServerMap).Returns(new VBucketServerMap());

            var mockConnectionPool = new Mock<IConnectionPool>();
            var mockIoService = new Mock<IIOService>();
            mockIoService.Setup(x => x.ConnectionPool).Returns(mockConnectionPool.Object);
            mockIoService.Setup(x => x.SupportsSubdocXAttributes).Returns(true);
            var mockSasl = new Mock<ISaslMechanism>();

            var clientConfig = new ClientConfiguration();
            var context = new CouchbaseConfigContext(
                mockBucketConfig.Object,
                clientConfig,
                p => mockIoService.Object,
                (a, b) => mockConnectionPool.Object,
                (a, b, c, d) => mockSasl.Object,
                new DefaultTranscoder(),
                null, null);

            context.LoadConfig(mockIoService.Object);

            Assert.IsTrue(context.SupportsSubdocXAttributes);
        }

        [Test]
        public void When_Loading_Config_UseErrorMap_Is_True_If_IOService_Indicates_It_Is_Enabled()
        {
            var mockBucketConfig = new Mock<IBucketConfig>();
            mockBucketConfig.Setup(x => x.Name).Returns("default");
            mockBucketConfig.Setup(x => x.Nodes).Returns(new[] { new Node { Hostname = "127.0.0.1" } });
            mockBucketConfig.Setup(x => x.VBucketServerMap).Returns(new VBucketServerMap());

            var mockConnectionPool = new Mock<IConnectionPool>();
            var mockIoService = new Mock<IIOService>();
            mockIoService.Setup(x => x.ConnectionPool).Returns(mockConnectionPool.Object);
            mockIoService.Setup(x => x.SupportsKvErrorMap).Returns(true);
            var mockSasl = new Mock<ISaslMechanism>();

            var clientConfig = new ClientConfiguration();
            var context = new CouchbaseConfigContext(
                mockBucketConfig.Object,
                clientConfig,
                p => mockIoService.Object,
                (a, b) => mockConnectionPool.Object,
                (a, b, c, d) => mockSasl.Object,
                new DefaultTranscoder(),
                null, null);

            context.LoadConfig();

            Assert.IsTrue(context.SupportsKvErrorMap);
        }

        [Test]
        public void When_Loading_Config_With_BucketConfig_UseErrorMap_Is_True_If_IOService_Indicates_It_Is_Enabled()
        {
            var mockBucketConfig = new Mock<IBucketConfig>();
            mockBucketConfig.Setup(x => x.Name).Returns("default");
            mockBucketConfig.Setup(x => x.Nodes).Returns(new[] { new Node { Hostname = "127.0.0.1" } });
            mockBucketConfig.Setup(x => x.VBucketServerMap).Returns(new VBucketServerMap());

            var mockConnectionPool = new Mock<IConnectionPool>();
            var mockIoService = new Mock<IIOService>();
            mockIoService.Setup(x => x.ConnectionPool).Returns(mockConnectionPool.Object);
            mockIoService.Setup(x => x.SupportsKvErrorMap).Returns(true);
            var mockSasl = new Mock<ISaslMechanism>();

            var clientConfig = new ClientConfiguration();
            var context = new CouchbaseConfigContext(
                mockBucketConfig.Object,
                clientConfig,
                p => mockIoService.Object,
                (a, b) => mockConnectionPool.Object,
                (a, b, c, d) => mockSasl.Object,
                new DefaultTranscoder(),
                null, null);

            context.LoadConfig(mockBucketConfig.Object);

            Assert.IsTrue(context.SupportsKvErrorMap);
        }

        [Test]
        public void When_Loading_Config_With_IOService_UseErrorMap_Is_True_If_IOService_Indicates_It_Is_Enabled()
        {
            var mockBucketConfig = new Mock<IBucketConfig>();
            mockBucketConfig.Setup(x => x.Name).Returns("default");
            mockBucketConfig.Setup(x => x.Nodes).Returns(new[] { new Node { Hostname = "127.0.0.1" } });
            mockBucketConfig.Setup(x => x.VBucketServerMap).Returns(new VBucketServerMap());

            var mockConnectionPool = new Mock<IConnectionPool>();
            var mockIoService = new Mock<IIOService>();
            mockIoService.Setup(x => x.ConnectionPool).Returns(mockConnectionPool.Object);
            mockIoService.Setup(x => x.SupportsKvErrorMap).Returns(true);
            var mockSasl = new Mock<ISaslMechanism>();

            var clientConfig = new ClientConfiguration();
            var context = new CouchbaseConfigContext(
                mockBucketConfig.Object,
                clientConfig,
                p => mockIoService.Object,
                (a, b) => mockConnectionPool.Object,
                (a, b, c, d) => mockSasl.Object,
                new DefaultTranscoder(),
                null, null);

            context.LoadConfig(mockIoService.Object);

            Assert.IsTrue(context.SupportsKvErrorMap);
        }

        [Test]
        public void LoadConfig_Accepts_IPv6_Addresses()
        {
            var serverConfigJson = ResourceHelper.ReadResource("config_with_ipv6");
            var serverConfig = JsonConvert.DeserializeObject<BucketConfig>(serverConfigJson);

            var mockConnectionPool = new Mock<IConnectionPool>();
            var mockIoService = new Mock<IIOService>();
            mockIoService.Setup(x => x.ConnectionPool).Returns(mockConnectionPool.Object);
            mockIoService.Setup(x => x.SupportsSubdocXAttributes).Returns(true);
            var mockSasl = new Mock<ISaslMechanism>();

            var clientConfig = new ClientConfiguration
            {
                BucketConfigs = new Dictionary<string, BucketConfiguration>
                {
                    {"samplebucket",
                    new BucketConfiguration{BucketName = "samplebucket"}}
                }
            };
            var context = new CouchbaseConfigContext(
                serverConfig,
                clientConfig,
                p => mockIoService.Object,
                (a, b) => mockConnectionPool.Object,
                (a, b, c, d) => mockSasl.Object,
                new DefaultTranscoder(),
                null, null);

            context.LoadConfig(serverConfig);

            Assert.IsTrue(context.IsDataCapable);
        }

        [Test]
        public void LoadConfig_Resuses_Existing_Service_Uris()
        {
            var services = new Services { N1QL = 8093, Fts = 8094, Analytics = 8095 };
            var nodes = new List<Node> {new Node {Hostname = "127.0.0.1"}};
            var nodeExts = new List<NodeExt> {new NodeExt {Hostname = "127.0.0.1", Services = services}};

            var mockBucketConfig = new Mock<IBucketConfig>();
            mockBucketConfig.Setup(x => x.Name).Returns("default");
            mockBucketConfig.Setup(x => x.Nodes).Returns(nodes.ToArray);
            mockBucketConfig.Setup(x => x.NodesExt).Returns(nodeExts.ToArray);
            mockBucketConfig.Setup(x => x.VBucketServerMap).Returns(new VBucketServerMap());

            var mockConnectionPool = new Mock<IConnectionPool>();
            var mockIoService = new Mock<IIOService>();
            mockIoService.Setup(x => x.ConnectionPool).Returns(mockConnectionPool.Object);
            mockIoService.Setup(x => x.SupportsEnhancedDurability).Returns(true);
            var mockSasl = new Mock<ISaslMechanism>();

            var clientConfig = new ClientConfiguration();
            var context = new CouchbaseConfigContext(
                mockBucketConfig.Object,
                clientConfig,
                p => mockIoService.Object,
                (a, b) => mockConnectionPool.Object,
                (a, b, c, d) => mockSasl.Object,
                new DefaultTranscoder(),
                null, null);

            // load first config with single node
            context.LoadConfig(mockBucketConfig.Object);

            Assert.AreEqual(1, context.QueryUris.Count);
            Assert.IsTrue(context.QueryUris.Contains(new Uri("http://127.0.0.1:8093/query")));

            Assert.AreEqual(1, context.SearchUris.Count);
            Assert.IsTrue(context.SearchUris.Contains(new Uri("http://127.0.0.1:8094/pools")));

            Assert.AreEqual(1, context.AnalyticsUris.Count);
            Assert.IsTrue(context.AnalyticsUris.Contains(new Uri("http://127.0.0.1:8095/analytics/service")));

            // add extra node to config, keeping existing
            nodes.Add(new Node {Hostname = "127.0.0.2"});
            nodeExts.Add(new NodeExt {Hostname = "127.0.0.2", Services = services});

            // create new bucket config, with extra node
            mockBucketConfig.Setup(x => x.Nodes).Returns(nodes.ToArray);

            // need to force because internal bucketconfig ref will be pointing to mock
            context.LoadConfig(mockBucketConfig.Object, true);

            Assert.AreEqual(2, context.QueryUris.Count);
            Assert.IsTrue(context.QueryUris.Contains(new Uri("http://127.0.0.1:8093/query")));
            Assert.IsTrue(context.QueryUris.Contains(new Uri("http://127.0.0.2:8093/query")));

            Assert.AreEqual(2, context.SearchUris.Count);
            Assert.IsTrue(context.SearchUris.Contains(new Uri("http://127.0.0.1:8094/pools")));
            Assert.IsTrue(context.SearchUris.Contains(new Uri("http://127.0.0.2:8094/pools")));

            Assert.AreEqual(2, context.AnalyticsUris.Count);
            Assert.IsTrue(context.AnalyticsUris.Contains(new Uri("http://127.0.0.1:8095/analytics/service")));
            Assert.IsTrue(context.AnalyticsUris.Contains(new Uri("http://127.0.0.2:8095/analytics/service")));
        }

        [Test]
        public void LoadConfig_When_Reused_Server_Use_Latest_NodeAdapter_Settings()
        {
            var initialConfig = JsonConvert.DeserializeObject<BucketConfig>(
                ResourceHelper.ReadResource(@"Data\config-rev-46.json"));

            var mockConnectionPool = new Mock<IConnectionPool>();
            var mockIoService = new Mock<IIOService>();
            mockIoService.Setup(x => x.ConnectionPool).Returns(mockConnectionPool.Object);
            mockIoService.Setup(x => x.SupportsEnhancedDurability).Returns(true);
            var mockSasl = new Mock<ISaslMechanism>();

            var clientConfig = new ClientConfiguration();
            var context = new CouchbaseConfigContext(
                initialConfig,
                clientConfig,
                p => mockIoService.Object,
                (a, b) => mockConnectionPool.Object,
                (a, b, c, d) => mockSasl.Object,
                new DefaultTranscoder(),
                null, null);

            context.LoadConfig();
            var server = context.GetServers().First(x => Equals(x.EndPoint, IPEndPointExtensions.GetEndPoint("172.23.120.212", 0)));
            Assert.IsFalse(server.IsQueryNode);

            var newConfig = JsonConvert.DeserializeObject<BucketConfig>(
                ResourceHelper.ReadResource(@"Data\config-rev-60.json"));

            // load first config with single node
            context.LoadConfig(newConfig);

            server = context.GetServers().First(x => Equals(x.EndPoint, IPEndPointExtensions.GetEndPoint("172.23.120.212", 0)));
            Assert.IsTrue(server.IsQueryNode);
        }

        [Test]
        public void LoadConfig_when_reused_updates_bucket_revision()
        {
            var initialConfig = JsonConvert.DeserializeObject<BucketConfig>(ResourceHelper.ReadResource(@"Data\config-rev-46.json"));

            var mockConnectionPool = new Mock<IConnectionPool>();
            var mockIoService = new Mock<IIOService>();
            mockIoService.Setup(x => x.ConnectionPool).Returns(mockConnectionPool.Object);
            mockIoService.Setup(x => x.SupportsEnhancedDurability).Returns(true);
            var mockSasl = new Mock<ISaslMechanism>();

            var clientConfig = new ClientConfiguration();
            var context = new CouchbaseConfigContext(
                initialConfig,
                clientConfig,
                p => mockIoService.Object,
                (a, b) => mockConnectionPool.Object,
                (a, b, c, d) => mockSasl.Object,
                new DefaultTranscoder(),
                null, null);

            // load using initial config
            context.LoadConfig();
            Assert.IsTrue(context.GetServers().All(s => s.Revision == initialConfig.Rev));

            // load new config
            var newConfig = JsonConvert.DeserializeObject<BucketConfig>(ResourceHelper.ReadResource(@"Data\config-rev-60.json"));
            context.LoadConfig(newConfig);

            //server = context.GetServers().First(x => Equals(x.EndPoint, IPEndPointExtensions.GetEndPoint("172.23.120.212", 0)));
            Assert.IsTrue(context.GetServers().All(s => s.Revision == newConfig.Rev));
        }
    }
}
