using System;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Providers;
using Couchbase.Configuration.Server.Providers.Streaming;
using Couchbase.IO;
using Couchbase.IO.Strategies;
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
                (pool, sasl) => new DefaultIOStrategy(pool, sasl),
                (config, endpoint) => new ConnectionPool<EapConnection>(config, endpoint),
                SaslFactory.GetFactory3());
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

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion