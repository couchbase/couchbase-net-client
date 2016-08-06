using System.IO;
using Couchbase.Configuration.Server.Serialization;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.Tests.Configuration.Server.Serialization
{
    [TestFixture]
    public class NodeArrayExtensionsTests
    {
        [Test]
        public void When_Nodes_And_ServersList_Are_Out_Of_Order_Reorder_Servers_To_ServersList()
        {
            var json = ResourceHelper.ReadResource(@"Data\Configuration\config-nodes-and-serverslist-out-of-order.json");
            var bucket = JsonConvert.DeserializeObject<BucketConfig>(json);

            Assert.AreEqual(bucket.Nodes[0].Hostname.Split(':')[0], bucket.VBucketServerMap.ServerList[0].Split(':')[0]);
            Assert.AreNotEqual(bucket.Nodes[1].Hostname.Split(':')[0], bucket.VBucketServerMap.ServerList[1].Split(':')[0]);
            Assert.AreNotEqual(bucket.Nodes[2].Hostname.Split(':')[0], bucket.VBucketServerMap.ServerList[2].Split(':')[0]);
            Assert.AreNotEqual(bucket.Nodes[3].Hostname.Split(':')[0], bucket.VBucketServerMap.ServerList[3].Split(':')[0]);

            var nodes = bucket.Nodes.ReorderToServerList(bucket.VBucketServerMap);

            Assert.AreEqual(nodes[0].Hostname.Split(':')[0], bucket.VBucketServerMap.ServerList[0].Split(':')[0]);
            Assert.AreEqual(nodes[1].Hostname.Split(':')[0], bucket.VBucketServerMap.ServerList[1].Split(':')[0]);
            Assert.AreEqual(nodes[2].Hostname.Split(':')[0], bucket.VBucketServerMap.ServerList[2].Split(':')[0]);
            Assert.AreEqual(nodes[3].Hostname.Split(':')[0], bucket.VBucketServerMap.ServerList[3].Split(':')[0]);
        }

        [Test]
        public void When_ServerList_IsEmpty_Nodes_Are_Returned()
        {
            var json = ResourceHelper.ReadResource(@"Data\Configuration\config-nodes-and-serverslist-out-of-order.json");
            var bucket = JsonConvert.DeserializeObject<BucketConfig>(json);
            bucket.VBucketServerMap.ServerList = new string[0];

            var nodes = bucket.Nodes.ReorderToServerList(bucket.VBucketServerMap);
            Assert.AreEqual(4, nodes.Length);
        }
    }
}
