using Couchbase.Core.Configuration.Server;
using Couchbase.UnitTests.Utils;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.UnitTests.Core.Configuration.Server
{
    public class BucketConfigExtensions
    {
        [Fact]
        public void When_Nodes_And_ServersList_Are_Out_Of_Order_Reorder_Servers_To_ServersList()
        {
            var json = ResourceHelper.ReadResource(@"Documents\configs\config-nodes-and-serverslist-out-of-order.json");
            var bucket = JsonConvert.DeserializeObject<BucketConfig>(json);

            Assert.Equal(bucket.Nodes[0].Hostname.Split(':')[0], bucket.VBucketServerMap.ServerList[0].Split(':')[0]);
            Assert.NotEqual(bucket.Nodes[1].Hostname.Split(':')[0], bucket.VBucketServerMap.ServerList[1].Split(':')[0]);
            Assert.NotEqual(bucket.Nodes[2].Hostname.Split(':')[0], bucket.VBucketServerMap.ServerList[2].Split(':')[0]);
            Assert.NotEqual(bucket.Nodes[3].Hostname.Split(':')[0], bucket.VBucketServerMap.ServerList[3].Split(':')[0]);

            var nodes = bucket.GetNodesOrderedToServerList();

            Assert.Equal(nodes[0].Hostname.Split(':')[0], bucket.VBucketServerMap.ServerList[0].Split(':')[0]);
            Assert.Equal(nodes[1].Hostname.Split(':')[0], bucket.VBucketServerMap.ServerList[1].Split(':')[0]);
            Assert.Equal(nodes[2].Hostname.Split(':')[0], bucket.VBucketServerMap.ServerList[2].Split(':')[0]);
            Assert.Equal(nodes[3].Hostname.Split(':')[0], bucket.VBucketServerMap.ServerList[3].Split(':')[0]);
        }

        [Fact]
        public void When_ServerList_IsEmpty_Nodes_Are_Returned()
        {
            var json = ResourceHelper.ReadResource(@"Documents\configs\config-nodes-and-serverslist-out-of-order.json");
            var bucket = JsonConvert.DeserializeObject<BucketConfig>(json);
            bucket.VBucketServerMap.ServerList = new string[0];

            var nodes = bucket.GetNodesOrderedToServerList();
            Assert.Equal(4, nodes.Count);
        }
    }
}
