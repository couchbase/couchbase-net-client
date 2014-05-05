using System;
using Couchbase.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Providers;
using Couchbase.Configuration.Server.Providers.Streaming;
using Couchbase.IO;
using Couchbase.IO.Strategies.Async;
using NUnit.Framework;

namespace Couchbase.Tests.Configuration.Server.Providers.Streaming
{
    [TestFixture]
    internal class HttpStreamingProviderTests : IConfigObserver
    {
        private IConfigProvider _provider;
        private const string BucketName = "default";

        [TestFixtureSetUp]
        public void SetUp()
        {
            var configuration = new ClientConfiguration();
            _provider = new HttpStreamingProvider(
                configuration,
                pool => new SocketAsyncStrategy(pool), 
                (config, endpoint) => new DefaultConnectionPool(config, endpoint));
        }

        [Test]
        public void Test_GetConfig()
        {
            var configInfo = _provider.GetConfig("default");
            Assert.IsNotNull(configInfo);
            Console.WriteLine("GetConfig");
        }

        [Test]
        public void Test_GetCached()
        {
            var configInfo = _provider.GetConfig("default");
            var cachedConfig = _provider.GetCached("default");

            Assert.AreEqual(cachedConfig, configInfo);
        }

       [Test]
        public void Test_RegisterListener()
        {
           //we need to initialize the internal collections
           var configInfo = _provider.GetConfig("default");

           _provider.RegisterObserver(this);
           var exists = _provider.ObserverExists(this);
           
           Assert.IsTrue(exists);

           //if this isn't unregistered, the thread will continue forever
           _provider.UnRegisterObserver(this);
    
        }

        [Test] 
        public void Test_UnRegisterListener()
        {
            //we need to initialize the internal collections
            var configInfo = _provider.GetConfig("default");

            _provider.RegisterObserver(this);
            _provider.UnRegisterObserver(this);

            var exists = _provider.ObserverExists(this);
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

        [TestFixtureTearDown]
        public void TearDown()
        {
            _provider.Dispose();
        }

        public void Dispose()
        {
            //NOOP
        }
    }
}
