using Couchbase.Authentication.SASL;
using Couchbase.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Moq;
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
                new DefaultTranscoder());

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
                new DefaultTranscoder());

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
                new DefaultTranscoder());

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
                new DefaultTranscoder());

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
                new DefaultTranscoder());

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
                new DefaultTranscoder());

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
                new DefaultTranscoder());

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
                new DefaultTranscoder());

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
                new DefaultTranscoder());

            context.LoadConfig(mockIoService.Object);

            Assert.IsTrue(context.SupportsKvErrorMap);
        }
    }
}
