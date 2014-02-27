using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Server.Serialization;
using NUnit.Framework;

namespace Couchbase.Tests.Configuration.Server.Serialization
{
    [TestFixture]
    public class VBucketServerMapTests
    {
        private VBucketServerMap _vBucketServerMap1;
        private VBucketServerMap _vBucketServerMap2;
        private VBucketServerMap _vBucketServerMap3;

        [TestFixtureSetUp]
        public void Setup()
        {
            _vBucketServerMap1 = new VBucketServerMap
            {
                HashAlgorithm = "CRC",
                NumReplicas = 1,
                ServerList = new[] { "192.168.56.101:11210", "192.168.56.104:11210" },
                VBucketMap = new int[][] { new[] { 1, 0 }, new[] { 1, 0 }, new[] { 1, 0 } }
            };

            _vBucketServerMap2 = new VBucketServerMap
            {
                HashAlgorithm = "CRC",
                NumReplicas = 1,
                ServerList = new[] { "192.168.56.101:11210", "192.168.56.104:11210" },
                VBucketMap = new int[][] { new[] { 1, 0 }, new[] { 1, 0 }, new[] { 1, 0 } }
            };

            _vBucketServerMap3 = new VBucketServerMap
            {
                HashAlgorithm = "CRC",
                NumReplicas = 1,
                ServerList = new[] { "192.168.56.101:11210", "192.168.56.103:11210" },
                VBucketMap = new int[][] { new[] { 0, 1 }, new[] { 1, 0 }, new[] { 1, 0 } }
            };
        }

        [Test]
        public void Test_GetHashcode()
        {
            Assert.AreEqual(_vBucketServerMap1.GetHashCode(), _vBucketServerMap2.GetHashCode());
            Assert.AreNotEqual(_vBucketServerMap1.GetHashCode(), _vBucketServerMap3.GetHashCode());
        }

        [Test]
        public void Test_Equals()
        {
            Assert.IsTrue(_vBucketServerMap1.Equals(_vBucketServerMap2));
            Assert.IsFalse(_vBucketServerMap1.Equals(_vBucketServerMap3));
        }
    }
}
