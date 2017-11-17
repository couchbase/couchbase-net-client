using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Authentication;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Providers;
using Couchbase.IO;
using Couchbase.IO.Services;
using Couchbase.Utils;
using Moq;
using Newtonsoft.Json.Linq;
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
        public void Test_That_ClientConfiguration_Is_NotNull()
        {
            var config = new ClientConfiguration();
            config.UseSsl = false;

            config.BucketConfigs.Remove("default");
            BucketConfiguration bucketConfiguration = new BucketConfiguration();
            bucketConfiguration.BucketName = "default";
            bucketConfiguration.Password = "password";
            bucketConfiguration.UseSsl = false;
            bucketConfiguration.UseEnhancedDurability = true;
            config.Servers.Clear();

            config.Servers.Add(
                new UriBuilder("http://",
                    "127.0.0.1",
                    8091,
                    "pools").Uri
            );


            config.PoolConfiguration.ShutdownTimeout = 15;
            bucketConfiguration.Servers.Clear(); //we remove default localhost server
            config.BucketConfigs.Add("default",
                bucketConfiguration);

            config.Initialize();

            foreach (var bucketConfigurationServer in bucketConfiguration.Servers)
            {
                var bucketConfig = config.BucketConfigs["default"];
                var cloned = bucketConfig.PoolConfiguration.Clone(bucketConfigurationServer);
                Assert.IsNotNull(cloned.ClientConfiguration);
            }
        }

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
        public void When_UseConnectionPooling_Is_False_IOServiceFactory_Returns_SharedPooledIOService()
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
            mockConnectionPool.Setup(x => x.Configuration).Returns(new PoolConfiguration());
            mockConnectionPool.Setup(x => x.Connections).Returns(new List<IConnection> {mockConnection.Object});

            var service = clientConfig.IOServiceCreator.Invoke(mockConnectionPool.Object);

            Assert.IsInstanceOf<SharedPooledIOService>(service);
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
            mockConnectionPool.Setup(x => x.Configuration).Returns(new PoolConfiguration());
            mockConnectionPool.Setup(x => x.Connections).Returns(new List<IConnection> {mockConnection.Object});

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
            connectionPool.Setup(x => x.Configuration).Returns(new PoolConfiguration());
            connectionPool.Setup(x => x.Connections).Returns(new List<IConnection> {conn.Object});

            var service = config.IOServiceCreator(connectionPool.Object);

            Assert.IsInstanceOf<PooledIOService>(service);
        }

        [Test]
        public void When_UseSsl_Is_False_IOServiceFactory_Returns_SharedPooledIOService()
        {
            var config = new ClientConfiguration
            {
                UseSsl = false
            };

            var conn = new Mock<IConnection>();
            conn.Setup(x => x.IsAuthenticated).Returns(true);

            var connectionPool = new Mock<IConnectionPool>();
            connectionPool.Setup(x => x.Acquire()).Returns(conn.Object);
            connectionPool.Setup(x => x.Configuration).Returns(new PoolConfiguration());
            connectionPool.Setup(x => x.Connections).Returns(new List<IConnection> {conn.Object});

            var service = config.IOServiceCreator(connectionPool.Object);
            Assert.IsInstanceOf<SharedPooledIOService>(service);
        }

        [Test]
        public void When_Defaults_Are_Used_IOServiceFactory_Returns_SharedPooledIOService()
        {
            var config = new ClientConfiguration();

            var conn = new Mock<IConnection>();
            conn.Setup(x => x.IsAuthenticated).Returns(true);

            var connectionPool = new Mock<IConnectionPool>();
            connectionPool.Setup(x => x.Acquire()).Returns(conn.Object);
            connectionPool.Setup(x => x.Configuration).Returns(new PoolConfiguration());
            connectionPool.Setup(x => x.Connections).Returns(new List<IConnection> {conn.Object});

            var service = config.IOServiceCreator(connectionPool.Object);
            Assert.IsInstanceOf<SharedPooledIOService>(service);
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
        public void Throws_Argument_Exception_If_Connection_Values_Are_Not_Valid_For_BucketConfigs(int maxSize,
            int minSize)
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
            var clientConfig =
                new ClientConfiguration(
                    (CouchbaseClientSection) ConfigurationManager.GetSection("couchbaseClients/couchbase"));

            //assert
            Assert.IsTrue(clientConfig.BucketConfigs["beer-sample"].UseEnhancedDurability);
        }

        [Test]
        public void
            BucketConfiguration_NoPoolConfigurationDefinedAndUseEnhancedDurabilityIsFalse_UseEnhancedDurabilityIsFalse()
        {
            //arrange/act
            var clientConfig =
                new ClientConfiguration(
                    (CouchbaseClientSection) ConfigurationManager.GetSection("couchbaseClients/couchbase"));

            //assert
            Assert.IsFalse(clientConfig.BucketConfigs["default1"].UseEnhancedDurability);
        }

        [Test]
        public void BucketConfiguration_NoPoolConfigurationDefinedAndUseSsl_UseSslIsTrue()
        {
            //arrange/act
            var clientConfig =
                new ClientConfiguration(
                    (CouchbaseClientSection) ConfigurationManager.GetSection("couchbaseClients/couchbase"));

            //assert
            Assert.IsTrue(clientConfig.BucketConfigs["beer-sample"].UseSsl);
        }

        [Test]
        public void BucketConfiguration_NoPoolConfigurationDefinedAndUseSsl_UseSslIsFalse()
        {
            //arrange/act
            var clientConfig =
                new ClientConfiguration(
                    (CouchbaseClientSection) ConfigurationManager.GetSection("couchbaseClients/couchbase"));

            //assert
            Assert.IsFalse(clientConfig.BucketConfigs["default1"].UseSsl);
        }

        [Test]
        public void BucketConfiguration_PoolConfigurationDefined_UsesConfiguredSettings()
        {
            //arrange/act
            var clientConfig =
                new ClientConfiguration(
                    (CouchbaseClientSection) ConfigurationManager.GetSection("couchbaseClients/couchbase"));

            //assert
            Assert.AreEqual(15, clientConfig.BucketConfigs["default1"].PoolConfiguration.MinSize);
            Assert.AreEqual(20, clientConfig.BucketConfigs["default1"].PoolConfiguration.MaxSize);
        }

        [Test]
        public void BucketConfiguration_NoPoolConfigurationDefined_UsesDerivedPoolSettings()
        {
            //arrange/act
            var clientConfig =
                new ClientConfiguration(
                    (CouchbaseClientSection) ConfigurationManager.GetSection("couchbaseClients/couchbase"));

            //assert
            Assert.AreEqual(5, clientConfig.BucketConfigs["beer-sample"].PoolConfiguration.MinSize);
            Assert.AreEqual(10, clientConfig.BucketConfigs["beer-sample"].PoolConfiguration.MaxSize);
        }

        [Test]
        public void ClientConfiguration_IgnoreHostnameValidation()
        {
            //arrange/act
            var clientConfig =
                new ClientConfiguration(
                    (CouchbaseClientSection) ConfigurationManager.GetSection("couchbaseClients/couchbase"));

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
            var config =
                new ClientConfiguration(
                    (CouchbaseClientSection) ConfigurationManager.GetSection("couchbaseClients/couchbase"));

            Assert.AreEqual(100, config.VBucketRetrySleepTime);
        }

        [Test]
        public void ClientConfigSection_Get_Username_Password_From_Config()
        {
            var config =
                new ClientConfiguration(
                    (CouchbaseClientSection) ConfigurationManager.GetSection("couchbaseClients/secure"));
            var authenticator = config.Authenticator as PasswordAuthenticator;

            Assert.IsNotNull(authenticator);
            Assert.AreEqual("CustomUser", authenticator.Username);
            Assert.AreEqual("p@ssW0rd", authenticator.Password);
        }

        [Test]
        public void ClientConfigSection_Username_From_ConnectionString()
        {
            var config =
                new ClientConfiguration(
                    (CouchbaseClientSection) ConfigurationManager.GetSection(
                        "couchbaseClients/secureConnectionString"));
            var authenticator = config.Authenticator as PasswordAuthenticator;

            Assert.IsNotNull(authenticator);
            Assert.AreEqual("CustomUser", authenticator.Username);
            Assert.AreEqual("p@ssW0rd", authenticator.Password);
        }
#endif

        [Test]
        public void
            When_HeartbeatConfigInterval_Is_Less_Than_HeartbeatConfigCheckFloor_Throw_ArgumentOutOfRangeException()
        {
            var clientConfig = new ClientConfiguration
            {
                HeartbeatConfigCheckFloor = 500,
                HeartbeatConfigInterval = 10
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => clientConfig.Initialize());
        }

        [Test]
        public void When_HeartbeatConfigInterval_Are_Equal_HeartbeatConfigCheckFloor_Throw_ArgumentOutOfRangeException()
        {
            var clientConfig = new ClientConfiguration
            {
                HeartbeatConfigCheckFloor = 500,
                HeartbeatConfigInterval = 500
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => clientConfig.Initialize());
        }

        [Test]
        public void
            When_HeartbeatConfigInterval_Is_Greater_Than_HeartbeatConfigCheckFloor_DoNotThrow_ArgumentOutOfRangeException()
        {
            var clientConfig = new ClientConfiguration
            {
                HeartbeatConfigCheckFloor = 10,
                HeartbeatConfigInterval = 500
            };

            Assert.DoesNotThrow(() => clientConfig.Initialize());
        }

        [Test]
        public void ClientConfiguration_HeartbeachConfigCheckInterval_Defaults_To_2500()
        {
            var clientConfig = new ClientConfiguration();
            Assert.AreEqual(2500, clientConfig.HeartbeatConfigInterval);
        }

#if NET45
        [Test]
        public void CouchbaseClientSection_HeartbeachConfigCheckInterval_Defaults_To_2500()
        {
            var clientConfig =
                new ClientConfiguration(
                    (CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase"));

            Assert.AreEqual(2500, clientConfig.HeartbeatConfigInterval);
        }
#endif

        [Test]
        public void When_HeartbeatConfigInterval_Is_Set_ConfigPollInterval_Is_Same()
        {
            var config = new ClientConfiguration
            {
                HeartbeatConfigInterval = 1000
            };
            config.Initialize();

            Assert.AreEqual(1000, config.ConfigPollInterval);
            Assert.AreEqual(config.ConfigPollInterval, config.HeartbeatConfigInterval);
        }

        [Test]
        public void When_HeartbeatConfigCheckFloor_Is_Set_ConfigPollFloor_Is_Same()
        {
            var config = new ClientConfiguration
            {
                HeartbeatConfigCheckFloor = 100
            };
            config.Initialize();

            Assert.AreEqual(100, config.ConfigPollCheckFloor);
            Assert.AreEqual(config.ConfigPollCheckFloor, config.HeartbeatConfigCheckFloor);
        }

        [Test]
        public void When_EnableConfigHeartBeatr_Is_Set_ConfigPollEnabled_Is_Same()
        {
            var config = new ClientConfiguration
            {
                EnableConfigHeartBeat = false
            };
            config.Initialize();

            Assert.AreEqual(false, config.ConfigPollEnabled);
            Assert.AreEqual(config.ConfigPollEnabled, config.EnableConfigHeartBeat);
        }

        [Test]
        public void CouchbaseClientDefinition_CorrectDefault()
        {
            var definition = new CouchbaseClientDefinition();

            var config = new ClientConfiguration(definition);
            config.Initialize();

            Assert.AreEqual(ServerConfigurationProviders.CarrierPublication | ServerConfigurationProviders.HttpStreaming,
                config.ConfigurationProviders);
        }

        [Test]
        [TestCase(ServerConfigurationProviders.CarrierPublication)]
        [TestCase(ServerConfigurationProviders.HttpStreaming)]
        [TestCase(ServerConfigurationProviders.CarrierPublication | ServerConfigurationProviders.HttpStreaming)]
        public void CouchbaseClientDefinition_ConfigurationProviders_Passthrough(ServerConfigurationProviders configurationProviders)
        {
            var definition = new CouchbaseClientDefinition
            {
                ConfigurationProviders = configurationProviders
            };

            var config = new ClientConfiguration(definition);
            config.Initialize();

            Assert.AreEqual(configurationProviders, config.ConfigurationProviders);
        }

        [Test]
        [TestCase("http://10.111.170.102:8091")]
        [TestCase("http://[fd63:6f75:6368:2068:1471:75ff:fe25:a8be]:8091")]
        public void Initialize_Supports_IPv6_Uri(string uri)
        {
            var config = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                   new Uri(uri)
                }
            };

            config.Initialize();
        }

#if NET45
        [Test]
        public void CouchbaseConfigurationSection_CorrectDefault()
        {
            var section = new CouchbaseClientSection();

            var config = new ClientConfiguration(section);
            config.Initialize();

            Assert.AreEqual(ServerConfigurationProviders.CarrierPublication | ServerConfigurationProviders.HttpStreaming,
                config.ConfigurationProviders);
        }

        [Test]
        [TestCase(ServerConfigurationProviders.CarrierPublication)]
        [TestCase(ServerConfigurationProviders.HttpStreaming)]
        [TestCase(ServerConfigurationProviders.CarrierPublication | ServerConfigurationProviders.HttpStreaming)]
        public void CouchbaseConfigurationSection_ConfigurationProviders_Passthrough(ServerConfigurationProviders configurationProviders)
        {
            var section = new CouchbaseClientSection
            {
                ConfigurationProviders = configurationProviders
            };

            var config = new ClientConfiguration(section);
            config.Initialize();

            Assert.AreEqual(configurationProviders, config.ConfigurationProviders);
        }
#endif
    }
}
