using System;
using System.Collections;
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
using Couchbase.Tracing;
using Couchbase.Utils;
using Moq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using OpenTracing;
using OpenTracing.Noop;
#if NET452
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

#if NET452

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

#if NET452
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

#if NET452
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

        #region Response Time Observability

        [Test]
        public void CouchbaseConfiguration_OperationTracingEnabled_Default()
        {
            var config = new ClientConfiguration();
            Assert.IsTrue(config.OperationTracingEnabled);
        }

        [Test]
        public void CouchbaseConfiguration_Tracer_CorrectDefault()
        {
            var configuration = new ClientConfiguration();
            Assert.IsInstanceOf<ThresholdLoggingTracer>(configuration.Tracer);
        }

        [TestCase(true, typeof(ThresholdLoggingTracer))]
        [TestCase(false, typeof(NoopTracer))]
        public void CouchbaseConfiguration_ThresholdLoggingTracer_Using_Definition(bool value, Type expectedType)
        {
            var configuration = new ClientConfiguration
            {
                OperationTracingEnabled = value
            };

            Assert.IsInstanceOf(expectedType, configuration.Tracer);
        }

        [Test]
        public void CouchbaseConfiguration_Can_Set_Custom_Tracer()
        {
            var mockTracer = new Mock<ITracer>();
            var configurtion = new ClientConfiguration
            {
                Tracer = mockTracer.Object
            };

            Assert.AreSame(mockTracer.Object, configurtion.Tracer);
        }

        [Test]
        public void ThresholdLoggingTracer_has_correct_default_values()
        {
            var tracer = new ThresholdLoggingTracer();

            Assert.AreEqual(10000, tracer.Interval);
            Assert.AreEqual(10, tracer.SampleSize);

            Assert.AreEqual(500000, tracer.KvThreshold);
            Assert.AreEqual(1000000, tracer.ViewThreshold);
            Assert.AreEqual(1000000, tracer.N1qlThreshold);
            Assert.AreEqual(1000000, tracer.SearchThreshold);
            Assert.AreEqual(1000000, tracer.AnalyticsThreshold);
        }

        [Test]
        public void ThresholdLoggingTracer_can_override_defaults()
        {
            const int interval = 5000;
            const int sampleSize = 20;

            var tracer = new ThresholdLoggingTracer
            {
                Interval = interval,
                SampleSize = sampleSize,
                KvThreshold = 250000,
                ViewThreshold = 250000,
                N1qlThreshold = 250000,
                SearchThreshold = 250000,
                AnalyticsThreshold = 250000
            };

            Assert.AreEqual(interval, tracer.Interval);
            Assert.AreEqual(sampleSize, tracer.SampleSize);
            Assert.AreEqual(250000, tracer.KvThreshold);
            Assert.AreEqual(250000, tracer.ViewThreshold);
            Assert.AreEqual(250000, tracer.N1qlThreshold);
            Assert.AreEqual(250000, tracer.SearchThreshold);
            Assert.AreEqual(250000, tracer.AnalyticsThreshold);
        }

#if NET452
        [Test]
        public void OperationTracing_ClientSection_Default_Values()
        {
            var section = new CouchbaseClientSection();

            var config = new ClientConfiguration(section);

            Assert.IsInstanceOf<ThresholdLoggingTracer>(config.Tracer);

            var tracer = (ThresholdLoggingTracer)config.Tracer;
            Assert.AreEqual(10000, tracer.Interval);
            Assert.AreEqual(10, tracer.SampleSize);

            Assert.AreEqual(500000, tracer.KvThreshold);
            Assert.AreEqual(1000000, tracer.ViewThreshold);
            Assert.AreEqual(1000000, tracer.N1qlThreshold);
            Assert.AreEqual(1000000, tracer.SearchThreshold);
            Assert.AreEqual(1000000, tracer.AnalyticsThreshold);
        }

        [Test]
        public void OperationTracing_ClientSection_Can_Disable()
        {
            var section = new CouchbaseClientSection
            {
                OperationTracingEnabled = false
            };

            var config = new ClientConfiguration(section);
            Assert.IsInstanceOf<NoopTracer>(config.Tracer);
        }
#endif

        #endregion

        #region Orphaned Response Reporter

        [Test]
        public void CouchbaseConfiguration_OrphanedResponseLoggingEnabled_Default()
        {
            var config = new ClientConfiguration();
            Assert.IsTrue(config.OperationTracingEnabled);
        }

        [Test]
        public void CouchbaseConfiguration_OrphanedResponseLogger_CorrectDefault()
        {
            var configuration = new ClientConfiguration();
            Assert.IsInstanceOf<OrphanedResponseLogger>(configuration.OrphanedResponseLogger);
        }

        [TestCase(true, typeof(OrphanedResponseLogger))]
        [TestCase(false, typeof(NullOrphanedResponseLogger))]
        public void CouchbaseConfiguration_OrphanedResponseLogger_Using_Definition(bool value, Type expectedType)
        {
            var configuration = new ClientConfiguration
            {
                OrphanedResponseLoggingEnabled = value
            };

            Assert.IsInstanceOf(expectedType, configuration.OrphanedResponseLogger);
        }

        [Test]
        public void CouchbaseConfiguration_Can_Set_Custom_OrphanedResponseLogger()
        {
            var mockReporter = new Mock<IOrphanedResponseLogger>();
            var configurtion = new ClientConfiguration
            {
                OrphanedResponseLogger = mockReporter.Object
            };

            Assert.AreSame(mockReporter.Object, configurtion.OrphanedResponseLogger);
        }

        [Test]
        public void OrphanedResponseLogger_has_correct_default_values()
        {
            var reporter = new OrphanedResponseLogger();

            Assert.AreEqual(10000, reporter.Interval);
            Assert.AreEqual(10, reporter.SampleSize);
        }

        [Test]
        public void OrphanedResponseLogger_can_override_defaults()
        {
            const int interval = 5000;
            const int sampleSize = 20;
            var reporter = new OrphanedResponseLogger
            {
                Interval = interval,
                SampleSize = sampleSize
            };

            Assert.AreEqual(interval, reporter.Interval);
            Assert.AreEqual(sampleSize, reporter.SampleSize);
        }

        #endregion

        #region Connection String

        public static IEnumerable ConnectionStringReplacesServersCases
        {
            get
            {
                yield return new TestCaseData(new object[] {"couchbase://localhost", new[]
                {
                    new Uri("http://localhost:8091/")
                }});

                yield return new TestCaseData(new object[] {"couchbase://host1,host2:11215", new[]
                {
                    new Uri("http://host1:8091/"),
                    new Uri("http://host2:8091/")
                }});

                yield return new TestCaseData(new object[] {"couchbases://localhost", new[]
                {
                    new Uri("http://localhost:8091/")
                }});

                yield return new TestCaseData(new object[] {"http://localhost", new[]
                {
                    new Uri("http://localhost:8091/")
                }});

                yield return new TestCaseData(new object[] {"http://localhost:9091", new[]
                {
                    new Uri("http://localhost:9091/")
                }});
            }
        }

        [Test]
        [TestCaseSource(nameof(ConnectionStringReplacesServersCases))]
        public void ConnectionString_ReplacesServers(string connectionString, Uri[] expected)
        {
            // Arrange

            var definition = new CouchbaseClientDefinition
            {
                ConnectionString = connectionString,
                Servers = new List<Uri>
                {
                    new Uri("http://old1"),
                    new Uri("http://old2")
                }
            };

            // Act

            var configuration = new ClientConfiguration(definition);

            // Assert

            Assert.AreEqual(expected.Length, configuration.Servers.Count);

            for (var i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], configuration.Servers[i]);
            }
        }

        [Test]
        public void ConnectionString_CouchbaseWithoutPort_SetsDirectPortToDefault()
        {
            // Arrange

            var definition = new CouchbaseClientDefinition
            {
                ConnectionString = "couchbase://localhost",
                DirectPort = 10000
            };

            // Act

            var configuration = new ClientConfiguration(definition);

            // Assert

            Assert.AreEqual(11210, configuration.DirectPort);
        }

        [Test]
        public void ConnectionString_CouchbaseWithPort_SetsDirectPort()
        {
            // Arrange

            var definition = new CouchbaseClientDefinition
            {
                ConnectionString = "couchbase://localhost:11500",
                DirectPort = 10000
            };

            // Act

            var configuration = new ClientConfiguration(definition);

            // Assert

            Assert.AreEqual(11500, configuration.DirectPort);
        }

        [Test]
        public void ConnectionString_CouchbasesWithoutPort_SetsSslPortToDefault()
        {
            // Arrange

            var definition = new CouchbaseClientDefinition
            {
                ConnectionString = "couchbases://localhost",
                SslPort = 10000
            };

            // Act

            var configuration = new ClientConfiguration(definition);

            // Assert

            Assert.AreEqual(11207, configuration.SslPort);
        }

        [Test]
        public void ConnectionString_CouchbasesWithPort_SetsSslPort()
        {
            // Arrange

            var definition = new CouchbaseClientDefinition
            {
                ConnectionString = "couchbases://localhost:11500",
                SslPort = 10000
            };

            // Act

            var configuration = new ClientConfiguration(definition);

            // Assert

            Assert.AreEqual(11500, configuration.SslPort);
        }

        public static IEnumerable ConnectionStringSetsSslCases
        {
            get
            {
                yield return new TestCaseData(new object[] {"couchbase://localhost", false});
                yield return new TestCaseData(new object[] {"couchbases://localhost", true});
                yield return new TestCaseData(new object[] {"http://localhost", false});
            }
        }

        [Test]
        [TestCaseSource(nameof(ConnectionStringSetsSslCases))]
        public void ConnectionString_SetsSsl(string connectionString, bool expected)
        {
            // Arrange

            var definition = new CouchbaseClientDefinition
            {
                ConnectionString = connectionString,
                UseSsl = !expected
            };

            // Act

            var configuration = new ClientConfiguration(definition);

            // Assert

            Assert.AreEqual(expected, configuration.UseSsl);
            Assert.AreEqual(expected, configuration.PoolConfiguration.UseSsl);
        }

        public static IEnumerable ConnectionStringSetsConfigurationProvidersCases
        {
            get
            {
                yield return new TestCaseData(new object[] {"couchbase://localhost", ServerConfigurationProviders.CarrierPublication});
                yield return new TestCaseData(new object[] {"couchbases://localhost", ServerConfigurationProviders.CarrierPublication});
                yield return new TestCaseData(new object[] {"http://localhost", ServerConfigurationProviders.CarrierPublication | ServerConfigurationProviders.HttpStreaming});
            }
        }

        [Test]
        [TestCaseSource(nameof(ConnectionStringSetsConfigurationProvidersCases))]
        public void ConnectionString_SetsConfigurationProviders(string connectionString, ServerConfigurationProviders expected)
        {
            // Arrange

            var definition = new CouchbaseClientDefinition
            {
                ConnectionString = connectionString,
                ConfigurationProviders = ServerConfigurationProviders.None
            };

            // Act

            var configuration = new ClientConfiguration(definition);

            // Assert

            Assert.AreEqual(expected, configuration.ConfigurationProviders);
        }

        #endregion

        [Test]
        public void NetworkType_defaults_to_auto()
        {
            var config = new ClientConfiguration();
            Assert.AreEqual(NetworkTypes.Auto, config.NetworkType);
        }

#if NET452
        [TestCase(NetworkTypes.Auto)]
        [TestCase(NetworkTypes.External)]
        [TestCase(NetworkTypes.Default)]
        public void NetworkType_can_override_using_client_section(string networkType)
        {
            var section = new CouchbaseClientSection
            {
                NetworkType = networkType
            };

            var config = new ClientConfiguration(section);
            Assert.AreEqual(networkType, config.NetworkType);
        }
#endif
    }
}
