using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.Cryptography;
using Couchbase.IO;
using Couchbase.IO.Strategies.Awaitable;
using Couchbase.Tests.Helpers;
using NUnit.Framework;

namespace Couchbase.Tests
{
    [TestFixture]
    public class KeyMapperTests
    {
        const string Key = "XXXXX";
        private Dictionary<int, IVBucket> _vBuckets;
        private List<IServer> _servers;

        [TestFixtureSetUp]
        public void SetUp()
        {
            var bucket = ConfigUtil.ServerConfig.Buckets.First();
            var vBucketServerMap = bucket.VBucketServerMap;

            _servers = vBucketServerMap.
                ServerList.
                Select(server => new Server(ObjectFactory.CreateIOStrategy(server))).
                Cast<IServer>().
                ToList();

            _vBuckets = new Dictionary<int, IVBucket>();
            for (var i = 0; i < vBucketServerMap.VBucketMap.Length; i++)
            {
                var primary = vBucketServerMap.VBucketMap[i][0];
                var replica = vBucketServerMap.VBucketMap[i][1];
                var vBucket = new VBucket(_servers, i, primary, replica);
                _vBuckets[i] = vBucket;
            }
        }

        [Test]
        public void TestMapKey()
        {
            IKeyMapper mapper = new KeyMapper(_vBuckets);
            var vBucket = mapper.MapKey(Key);
            Assert.IsNotNull(vBucket);
        }

        [Test]
        public void Test_That_HashAlgorithm_Is_Not_Null()
        {
            IKeyMapper mapper = new KeyMapper(_vBuckets);
            Assert.IsNotNull(mapper.HashAlgorithm);
        }

        [Test]
        public void Test_That_HashAlgorithm_Default_Type_Is_Crc32()
        {
            IKeyMapper mapper = new KeyMapper(_vBuckets);
            Assert.IsInstanceOf<Crc32>(mapper.HashAlgorithm);
        }

        [Test]
        public void Test_That_HashAlgorithm_Default_Type_Can_Be_Overridden()
        {
            IKeyMapper mapper = new KeyMapper(new HMACMD5(),_vBuckets);
            Assert.IsInstanceOf<HMACMD5>(mapper.HashAlgorithm);
        }

        [Test(Description = "Note, will probably only work on localhost")]
        public void Test_That_Key_XXXXX_Maps_To_VBucket_389()
        {
            const int actual = 389;
            IKeyMapper mapper = new KeyMapper(_vBuckets);
            var vBucket = mapper.MapKey(Key);
            Assert.AreEqual(vBucket.Index, actual);
        }

        [TestFixtureTearDown]
        public void TestFixtureTearDown()
        {
            _servers.ForEach(x=>x.Dispose());
        }
    }
}
