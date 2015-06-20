using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Utils;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.Tests.Core
{
    [TestFixture]
    public  class NodeAdapterTests
    {
        [Test]
        public void When_LocalHost_And_CB251_Verify_Ports()
        {
            var bucketConfig =
                JsonConvert.DeserializeObject<BucketConfig>(
                    File.ReadAllText("Data\\Configuration\\carrier-publication-config.json"));

            var nodes = bucketConfig.GetNodes();
            Assert.AreEqual(8091, nodes[0].MgmtApi);
            Assert.AreEqual(8092, nodes[0].Views);
            Assert.AreEqual(18091, nodes[0].MgmtApiSsl);
            Assert.AreEqual(18092, nodes[0].ViewsSsl);
            Assert.AreEqual(11210, nodes[0].KeyValue);
            Assert.AreEqual(11207, nodes[0].KeyValueSsl);
            Assert.AreEqual(IPEndPointExtensions.GetEndPoint(@"localhost:8092"), nodes[0].GetIPEndPoint(8092));
        }

        [Test]
        public void When_Couchbase_Bucket_And_CB251_Verify_Ports()
        {
            var bucketConfig =
                JsonConvert.DeserializeObject<BucketConfig>(
                    File.ReadAllText("Data\\Configuration\\couchbase-2_5_1-couchbase_config.json"));

            var nodes = bucketConfig.GetNodes();
            foreach (var node in nodes)
            {
                Assert.AreEqual(8091, node.MgmtApi);
                Assert.AreEqual(8092, node.Views);
                Assert.AreEqual(18091, node.MgmtApiSsl);
                Assert.AreEqual(18092, node.ViewsSsl);
                Assert.AreEqual(11210, node.KeyValue);
                Assert.AreEqual(11207, node.KeyValueSsl);
            }
        }

        [Test]
        public void When_Memcached_Bucket_And_CB251_Verify_Ports()
        {
            var bucketConfig =
                JsonConvert.DeserializeObject<BucketConfig>(
                    File.ReadAllText("Data\\Configuration\\couchbase-2_5_1-memcached_config.json"));

            var nodes = bucketConfig.GetNodes();
            foreach (var node in nodes)
            {
                Assert.AreEqual(8091, node.MgmtApi);
                Assert.AreEqual(8092, node.Views);
                Assert.AreEqual(18091, node.MgmtApiSsl);
                Assert.AreEqual(18092, node.ViewsSsl);
                Assert.AreEqual(11210, node.KeyValue);
                Assert.AreEqual(11207, node.KeyValueSsl);
            }
        }
    }
}
