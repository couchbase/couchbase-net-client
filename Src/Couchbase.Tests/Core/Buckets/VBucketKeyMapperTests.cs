using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.Cryptography;
using Couchbase.Tests.Helpers;
using NUnit.Framework;

namespace Couchbase.Tests.Core.Buckets
{
    [TestFixture]
    public class VBucketKeyMapperTests
    {
        const string Key = "XXXXX";
        private Dictionary<int, IVBucket> _vBuckets;
        private List<IServer> _servers;
        private VBucketServerMap _vBucketServerMap;

        [TestFixtureSetUp]
        public void SetUp()
        {
            var bucket = ConfigUtil.ServerConfig.Buckets.First();
            _vBucketServerMap = bucket.VBucketServerMap;

            _servers = _vBucketServerMap.
                ServerList.
                Select(server => new Server(ObjectFactory.CreateIOStrategy(server))).
                Cast<IServer>().
                ToList();
        }

        [Test]
        public void TestMapKey()
        {
            IKeyMapper mapper = new VBucketKeyMapper(_servers, _vBucketServerMap);
            var vBucket = mapper.MapKey(Key);
            Assert.IsNotNull(vBucket);
        }

        [Test]
        public void Test_That_HashAlgorithm_Is_Not_Null()
        {
            IKeyMapper mapper = new VBucketKeyMapper(_servers, _vBucketServerMap);
            Assert.IsNotNull(mapper.HashAlgorithm);
        }

        [Test]
        public void Test_That_HashAlgorithm_Default_Type_Is_Crc32()
        {
            IKeyMapper mapper = new VBucketKeyMapper(_servers, _vBucketServerMap);
            Assert.IsInstanceOf<Crc32>(mapper.HashAlgorithm);
        }

        [Test]
        public void Test_That_HashAlgorithm_Default_Type_Can_Be_Overridden()
        {
            IKeyMapper mapper = new VBucketKeyMapper(new HMACMD5(), _servers, _vBucketServerMap);
            Assert.IsInstanceOf<HMACMD5>(mapper.HashAlgorithm);
        }

        [Test(Description = "Note, will probably only work on localhost")]
        public void Test_That_Key_XXXXX_Maps_To_VBucket_389()
        {
            const int actual = 389;
            IKeyMapper mapper = new VBucketKeyMapper(_servers, _vBucketServerMap);
            var vBucket = (IVBucket)mapper.MapKey(Key);
            Assert.AreEqual(vBucket.Index, actual);
        }

        [TestFixtureTearDown]
        public void TestFixtureTearDown()
        {
            _servers.ForEach(x=>x.Dispose());
        }
    }
}
