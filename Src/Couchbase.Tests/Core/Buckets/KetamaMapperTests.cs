using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.IO;
using Couchbase.IO.Strategies.Awaitable;
using Couchbase.Tests.Helpers;
using NUnit.Framework;

namespace Couchbase.Tests.Core.Buckets
{
    [TestFixture]
    public class KetamaMapperTests
    {
        private KetamaKeyMapper _keyMapper;
        private List<IServer> _servers;
        private VBucketServerMap _vBucketServerMap;
        
        [TestFixtureSetUp]
        public void SetUp()
        {
            var bucket = ConfigUtil.ServerConfig.Buckets.Find(x => x.BucketType == "memcached");
            _servers = bucket.Nodes.
                Select(node => new Server(ObjectFactory.CreateIOStrategy(node))).
                Cast<IServer>().
                ToList();

            _keyMapper = new KetamaKeyMapper(_servers);
        }

        [Test]
        public void Test_MapKey()
        {
            const string key = "foo";
            var node = _keyMapper.MapKey(key);
            Assert.IsNotNull(node);
        }

        [Test]
        public void Test_CalculateHash()
        {
            const string key = "foo";
            var hash = _keyMapper.GetHash(key);
            const uint expected = 3675831724;
            //3675831724
            Assert.AreEqual(hash, expected);
        }

        [Test]
        public void Test_GetIndex()
        {
            const string key = "foo";
            var hash = _keyMapper.GetHash(key);
            var index = _keyMapper.FindIndex(hash);
            //Assert.AreEqual(276, index);
            Assert.AreEqual(275, index);
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            
        }
    }
}
