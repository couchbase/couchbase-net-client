using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.Configuration
{
    [TestFixture]
    public class BucketConfigExtensionTests
    {
        [TestCase(true, 11207)]
        [TestCase(false, 11210)]
        public void Test_GetBucketServerMap(bool useSsl, int port)
        {
            var json = ResourceHelper.ReadResource(@"Data\cluster-map.json");
            var bucketConfig = JsonConvert.DeserializeObject<BucketConfig>(json);
            var vbBucketServerMap = bucketConfig.GetBucketServerMap(useSsl);

            foreach (var ipEndPoint in vbBucketServerMap.IPEndPoints)
            {
                Assert.AreEqual(port, ipEndPoint.Port);
            }
        }

        [Test]
        public void Test_KV_Enabled_Only_When_In_Nodes()
        {
            var json = ResourceHelper.ReadResource(@"Data\cbse-5827.json");
            var bucketConfig = JsonConvert.DeserializeObject<BucketConfig>(json);

            var nodes = bucketConfig.GetNodes();
            Assert.AreEqual(3, nodes.Count);
            Assert.IsTrue(nodes[0].IsDataNode);
            Assert.IsTrue(nodes[1].IsDataNode);
            Assert.IsFalse(nodes[2].IsDataNode);
        }
    }
}
