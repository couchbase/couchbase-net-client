using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Providers;
using Couchbase.Configuration.Server.Providers.CarrierPublication;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.IO;
using Couchbase.IO.Strategies.Async;
using Couchbase.IO.Strategies.Awaitable;
using NUnit.Framework;

namespace Couchbase.Tests.Configuration.Server.Providers.CarrierPublication
{
    [TestFixture]
    internal class CarrierPublicationProviderTests : IConfigListener
    {
        private CarrierPublicationProvider _provider;
        private const string BucketName = "default";

        [TestFixtureSetUp]
        public void SetUp()
        {
            var configuration = new ClientConfiguration();
            _provider = new CarrierPublicationProvider(
                configuration, 
                pool => new SocketAsyncStrategy(pool, new PlainTextMechanism(BucketName, string.Empty)), 
                (config, endpoint) => new DefaultConnectionPool(config, endpoint));
        }

        [Test]
        public void Test_RegisterListener()
        {
            _provider.RegisterListener(this);

            var exists = _provider.ListenerExists(this);
            Assert.IsTrue(exists);
        }

        [Test]
        public void Test_UnRegisterListener()
        {
            _provider.RegisterListener(this);
            _provider.UnRegisterListener(this);

            var exists = _provider.ListenerExists(this);
            Assert.IsFalse(exists);
        }

        public string Name
        {
            get { return BucketName; }
        }

        public void NotifyConfigChanged(IConfigInfo configInfo)
        {
            Assert.IsNotNull(configInfo);
        }

        [Test]
        public void Test_That_GetConfig_Returns_ConfigInfo()
        {
            var configInfo = _provider.GetConfig(BucketName);

            Assert.IsNotNull(configInfo);
            Assert.AreEqual(BucketName, configInfo.BucketConfig.Name);
        }

        [Test]
        public void Test_That_GetCached_Returns_CachedConfig()
        {
            var configInfo = _provider.GetConfig(BucketName);
            var cachedConfig = _provider.GetCached(BucketName);

            Assert.Greater(configInfo.BucketConfig.Rev, 0);
            Assert.Greater(cachedConfig.BucketConfig.Rev, 0);
            Assert.AreEqual(configInfo.BucketConfig.Rev, cachedConfig.BucketConfig.Rev);
        }
    }
}
