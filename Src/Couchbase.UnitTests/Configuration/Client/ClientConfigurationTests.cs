using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Client.Providers;
using NUnit.Framework;

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
    }
}
