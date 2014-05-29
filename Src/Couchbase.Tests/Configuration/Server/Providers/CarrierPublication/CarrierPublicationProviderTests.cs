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
using Couchbase.IO.Strategies;
using Couchbase.IO.Strategies.Async;
using Couchbase.IO.Strategies.Awaitable;
using NUnit.Framework;

namespace Couchbase.Tests.Configuration.Server.Providers.CarrierPublication
{
    [TestFixture]
    internal class CarrierPublicationProviderTests : IConfigObserver
    {
        private CarrierPublicationProvider _provider;
        private const string BucketName = "default";

        [TestFixtureSetUp]
        public void SetUp()
        {
            var configuration = new ClientConfiguration();
            _provider = new CarrierPublicationProvider(
                configuration, 
                (pool, sasl) => new DefaultIOStrategy(pool, null /*new PlainTextMechanism(BucketName, string.Empty)*/),
                (config, endpoint) => new ConnectionPool<EapConnection>(config, endpoint),
                SaslFactory.GetFactory3());
        }

        [Test]
        public void Test_RegisterListener()
        {
            _provider.RegisterObserver(this);

            var exists = _provider.ObserverExists(this);
            Assert.IsTrue(exists);
        }

        [Test]
        public void Test_UnRegisterListener()
        {
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