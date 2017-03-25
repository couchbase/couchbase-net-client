using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.IO;
using Couchbase.IO.Services;
using Couchbase.Utils;
using Moq;
using NUnit.Framework;

#if NET45
using System.Configuration;
using Couchbase.Configuration.Client.Providers;
#endif

namespace Couchbase.UnitTests.Configuration.Client
{
    [TestFixture]
    public class ClientConfigurationTests
    {
        [Test]
        public void When_TcpKeepAliveTime_Set_On_ClientConfiguration_Defaults_Are_Not_Used()
        {
            var clientConfig = new ClientConfiguration
            {
                TcpKeepAliveTime = 1000
            };

            clientConfig.Initialize();

            Assert.AreEqual(1000, clientConfig.TcpKeepAliveTime);
            Assert.AreEqual(1000, clientConfig.PoolConfiguration.TcpKeepAliveTime);
        }

        [Test]
        public void When_TcpKeepAliveTime_Set_On_PoolConfiguration_Defaults_Are_Not_Used()
        {
            var clientConfig = new ClientConfiguration();
            clientConfig.PoolConfiguration.TcpKeepAliveTime = 1000;

            clientConfig.Initialize();

            Assert.AreEqual(ClientConfiguration.Defaults.TcpKeepAliveTime, clientConfig.TcpKeepAliveTime);
            Assert.AreEqual(1000, clientConfig.PoolConfiguration.TcpKeepAliveTime);
        }

        [Test]
        public void When_EnableTcpKeepAlives_Disabled_On_ClientConfiguration_Defaults_Are_Not_Used()
        {
            var clientConfig = new ClientConfiguration
            {
                EnableTcpKeepAlives = false
            };

            clientConfig.Initialize();

            Assert.AreEqual(false, clientConfig.EnableTcpKeepAlives);
            Assert.AreEqual(false, clientConfig.PoolConfiguration.EnableTcpKeepAlives);
        }

        [Test]
        public void When_EnableTcpKeepAlives_Disabled_On_PoolConfiguration_Defaults_Are_Not_Used()
        {
            var clientConfig = new ClientConfiguration();
            clientConfig.PoolConfiguration.EnableTcpKeepAlives = false;

            clientConfig.Initialize();

            Assert.AreEqual(ClientConfiguration.Defaults.EnableTcpKeepAlives, clientConfig.EnableTcpKeepAlives);
            Assert.AreEqual(false, clientConfig.PoolConfiguration.EnableTcpKeepAlives);
        }

        [Test]
        public void When_TcpKeepAliveInterval_Set_On_ClientConfiguration_Defaults_Are_Not_Used()
        {
            var clientConfig = new ClientConfiguration
            {
                TcpKeepAliveInterval = 10
            };

            clientConfig.Initialize();

            Assert.AreEqual(10, clientConfig.TcpKeepAliveInterval);
            Assert.AreEqual(10, clientConfig.PoolConfiguration.TcpKeepAliveInterval);
        }

        [Test]
        public void When_TcpKeepAliveInterval_Set_On_PoolConfiguration_Defaults_Are_Not_Used()
        {
            var clientConfig = new ClientConfiguration();
            clientConfig.PoolConfiguration.TcpKeepAliveInterval = 10;

            clientConfig.Initialize();

            Assert.AreEqual(ClientConfiguration.Defaults.TcpKeepAliveInterval, clientConfig.TcpKeepAliveInterval);
            Assert.AreEqual(10, clientConfig.PoolConfiguration.TcpKeepAliveInterval);
        }

        [Test]
        public void POCO_NoServers_DefaultsToLocalhost()
        {
            // Arrange

            var clientDefinition = new CouchbaseClientDefinition()
            {
                Servers = null
            };

            // Act

            var clientConfig = new ClientConfiguration(clientDefinition);

            // Assert

            Assert.AreEqual(1, clientConfig.Servers.Count);
            Assert.AreEqual(ClientConfiguration.Defaults.Server, clientConfig.Servers.First());
        }

        [Test]
        public void When_UseConnectionPooling_Is_False_IOServiceFactory_Returns_MultiplexingIOService()
        {
            var definition = new CouchbaseClientDefinition
            {
                UseConnectionPooling = false
            };

            var clientConfig = new ClientConfiguration(definition);
            clientConfig.Initialize();

            var mockConnection = new Mock<IConnection>();
            mockConnection.Setup(x => x.IsAuthenticated).Returns(true);
            var mockConnectionPool = new Mock<IConnectionPool>();
            mockConnectionPool.Setup(x => x.Acquire()).Returns(mockConnection.Object);

            var service = clientConfig.IOServiceCreator.Invoke(mockConnectionPool.Object);

            Assert.IsInstanceOf<MultiplexingIOService>(service);
        }

        [Test]
        public void When_UseConnectionPooling_Is_True_IOServiceFactory_Returns_PooledIOService()
        {
            var definition = new CouchbaseClientDefinition
            {
                UseConnectionPooling = true
            };

            var clientConfig = new ClientConfiguration(definition);
            clientConfig.Initialize();

            var mockConnection = new Mock<IConnection>();
            mockConnection.Setup(x => x.IsAuthenticated).Returns(true);
            var mockConnectionPool = new Mock<IConnectionPool>();
            mockConnectionPool.Setup(x => x.Acquire()).Returns(mockConnection.Object);

            var service = clientConfig.IOServiceCreator.Invoke(mockConnectionPool.Object);

            Assert.IsInstanceOf<PooledIOService>(service);
        }

        [Test]
        public void When_UseSsl_Is_True_IOServiceFactory_Returns_PooledIOService()
        {
            var config = new ClientConfiguration
            {
                UseSsl = true
            };

            var conn = new Mock<IConnection>();
            conn.Setup(x => x.IsAuthenticated).Returns(true);

            var connectionPool = new Mock<IConnectionPool>();
            connectionPool.Setup(x => x.Acquire()).Returns(conn.Object);

            var service = config.IOServiceCreator(connectionPool.Object);
            Assert.IsInstanceOf<PooledIOService>(service);
        }

        [Test]
        public void When_UseSsl_Is_False_IOServiceFactory_Returns_MultiplexingIOService()
        {
            var config = new ClientConfiguration
            {
                UseSsl = false
            };

            var conn = new Mock<IConnection>();
            conn.Setup(x => x.IsAuthenticated).Returns(true);

            var connectionPool = new Mock<IConnectionPool>();
            connectionPool.Setup(x => x.Acquire()).Returns(conn.Object);

            var service = config.IOServiceCreator(connectionPool.Object);
            Assert.IsInstanceOf<MultiplexingIOService>(service);
        }

        [Test]
        public void When_Defaults_Are_Used_IOServiceFactory_Returns_MultiplexingIOService()
        {
            var config = new ClientConfiguration();

            var conn = new Mock<IConnection>();
            conn.Setup(x => x.IsAuthenticated).Returns(true);

            var connectionPool = new Mock<IConnectionPool>();
            connectionPool.Setup(x => x.Acquire()).Returns(conn.Object);

            var service = config.IOServiceCreator(connectionPool.Object);
            Assert.IsInstanceOf<MultiplexingIOService>(service);
        }

        [TestCase("http://localhost:80/pools")]
        [TestCase("http://localhost/")]
        [TestCase("http://localhost")]
        [TestCase("http://localhost:8091/")]
        [TestCase("http://localhost:8091/pools/")]
        [TestCase("http://localhost:8091")]
        public void Test_UriValidation(string uriToTest)
        {
            var config = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri(uriToTest)
                }
            };
            config.Initialize();
            Assert.AreEqual("http://localhost:8091/pools", config.Servers.First().ToString());
        }

        [TestCase(0, 1, Description = "MaxSize is less than 1")]
        [TestCase(501, 1, Description = "MaxSize is greater than 500")]
        [TestCase(1, -1, Description = "MinSize is less than 0")]
        [TestCase(501, 501, Description = "MinSize is greater than 500")]
        [TestCase(5, 10, Description = "Maxsize is greater than MinSize")]
        public void Throws_Argument_Exception_If_Connection_Values_Are_Not_Valid(int maxSize, int minSize)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ClientConfiguration
            {
                PoolConfiguration = new PoolConfiguration
                {
                    MaxSize = maxSize,
                    MinSize = minSize
                }
            }.Initialize());
        }

        [TestCase(0, 1, Description = "MaxSize is less than 1")]
        [TestCase(501, 1, Description = "MaxSize is greater than 500")]
        [TestCase(1, -1, Description = "MinSize is less than 0")]
        [TestCase(501, 501, Description = "MinSize is greater than 500")]
        [TestCase(5, 10, Description = "Maxsize is greater than MinSize")]
        public void Throws_Argument_Exception_If_Connection_Values_Are_Not_Valid_For_BucketConfigs(int maxSize, int minSize)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ClientConfiguration
            {
                BucketConfigs = new Dictionary<string, BucketConfiguration>
                {
                    {
                        "default", new BucketConfiguration
                        {
                            PoolConfiguration = new PoolConfiguration
                            {
                                MaxSize = maxSize,
                                MinSize = minSize
                            }
                        }
                    }
                }
            }.Initialize());
        }

#if NET45

        [Test]
        public void BucketConfiguration_NoPoolConfigurationDefinedAndUseEnhancedDurability_UseEnhancedDurabilityIsTrue()
        {
            //arrange/act
            var clientConfig = new ClientConfiguration((CouchbaseClientSection) ConfigurationManager.GetSection("couchbaseClients/couchbase"));

            //assert
            Assert.IsTrue(clientConfig.BucketConfigs["beer-sample"].UseEnhancedDurability);
        }

        [Test]
        public void BucketConfiguration_NoPoolConfigurationDefinedAndUseEnhancedDurabilityIsFalse_UseEnhancedDurabilityIsFalse()
        {
            //arrange/act
            var clientConfig = new ClientConfiguration((CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase"));

            //assert
            Assert.IsFalse(clientConfig.BucketConfigs["default1"].UseEnhancedDurability);
        }

        [Test]
        public void BucketConfiguration_NoPoolConfigurationDefinedAndUseSsl_UseSslIsTrue()
        {
            //arrange/act
            var clientConfig = new ClientConfiguration((CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase"));

            //assert
            Assert.IsTrue(clientConfig.BucketConfigs["beer-sample"].UseSsl);
        }

        [Test]
        public void BucketConfiguration_NoPoolConfigurationDefinedAndUseSsl_UseSslIsFalse()
        {
            //arrange/act
            var clientConfig = new ClientConfiguration((CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase"));

            //assert
            Assert.IsFalse(clientConfig.BucketConfigs["default1"].UseSsl);
        }

        [Test]
        public void BucketConfiguration_PoolConfigurationDefined_UsesConfiguredSettings()
        {
            //arrange/act
            var clientConfig = new ClientConfiguration((CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase"));

            //assert
            Assert.AreEqual(15, clientConfig.BucketConfigs["default1"].PoolConfiguration.MinSize);
            Assert.AreEqual(20, clientConfig.BucketConfigs["default1"].PoolConfiguration.MaxSize);
        }

        [Test]
        public void BucketConfiguration_NoPoolConfigurationDefined_UsesDerivedPoolSettings()
        {
            //arrange/act
            var clientConfig = new ClientConfiguration((CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase"));

            //assert
            Assert.AreEqual(5, clientConfig.BucketConfigs["beer-sample"].PoolConfiguration.MinSize);
            Assert.AreEqual(10, clientConfig.BucketConfigs["beer-sample"].PoolConfiguration.MaxSize);
        }

        [Test]
        public void ClientConfiguration_IgnoreHostnameValidation()
        {
            //arrange/act
            var clientConfig = new ClientConfiguration((CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase"));

            Assert.IsTrue(ClientConfiguration.IgnoreRemoteCertificateNameMismatch);
        }

        [Test]
        public void ClientConfiguration_VBucketRetrySleepTime_DefaultsTo100ms()
        {
            var config = new ClientConfiguration();

            Assert.AreEqual(100, config.VBucketRetrySleepTime);
        }

        [Test]
        public void ClientConfigSection_VBucketRetrySleepTime_DefaultsTo100ms()
        {
            //arrange/act
            var config = new ClientConfiguration((CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase"));

            Assert.AreEqual(100, config.VBucketRetrySleepTime);
        }
#endif
    }
}
