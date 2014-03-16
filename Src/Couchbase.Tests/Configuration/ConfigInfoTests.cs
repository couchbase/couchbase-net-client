using System.Collections.Generic;
using System.Linq;
using Couchbase.Configuration;
using Couchbase.Configuration.Server.Providers.FileSystem;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.Tests.Configuration.Client;
using NUnit.Framework;

namespace Couchbase.Tests.Configuration
{
    [TestFixture]
    public class ConfigInfoTests
    {
        private IConfigInfo _configInfo;

        [TestFixtureSetUp]
        public void SetUp()
        {
            var clientConfig = new FakeClientConfig();
            var serverConfig = new FileSystemConfig(clientConfig.BootstrapPath);
            serverConfig.Initialize();
            _configInfo = new ConfigInfo(serverConfig, clientConfig);
        }

        [Test]
        public void Test_GetKeyMapper()
        {
            var keyMapper = _configInfo.GetKeyMapper("default");
            Assert.IsNotNull(keyMapper);
        }

        [Test]
        public void Test_MapKey()
        {
            const string key = "TheKey";
            var keyMapper = _configInfo.GetKeyMapper("default");
            var vBucket = keyMapper.MapKey(key);    
            Assert.IsNotNull(vBucket);
        }

        [Test]
        public void Test_BucketType()
        {
            Assert.AreEqual(BucketTypeEnum.Couchbase, _configInfo.BucketType);
        }

        [Test]
        [ExpectedException(typeof(NullConfigException))]
        public void Test_BucketType_Throws_NullConfigException_When_Config_Is_Null()
        {
            var configInfo = new PersistentConfigContext(null, null);
            Assert.AreEqual(BucketTypeEnum.Couchbase, configInfo.BucketType);
        }
    }
}
