using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.UnitTests.Utils;
using Couchbase.Utils;
using Xunit;

namespace Couchbase.UnitTests.Core.Configuration.Server
{
    public class BucketConfigTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(512)]
        [InlineData(1023)]
        public void When_Rev_Same_And_VBucketMap_Different_Fail(int index)
        {
            var config1 = ResourceHelper.ReadResource<BucketConfig>(@"Documents\Configs\rev94.json");
            var config2 = ResourceHelper.ReadResource<BucketConfig>(@"Documents\Configs\rev94.json");

            var values = config2.VBucketServerMap.VBucketMap[index];
            config2.VBucketServerMap.VBucketMap[index] = new[] {values[1], values[0]};

            Assert.False(config2.Equals(config1));
        }

        [Fact]
        public void Test_Equals_True()
        {
            var config1 = ResourceHelper.ReadResource<BucketConfig>(@"Documents\Configs\rev94.json");
            var config2 = ResourceHelper.ReadResource<BucketConfig>(@"Documents\Configs\rev94.json");

            Assert.True(config2.Equals(config1));
        }

        [Fact]
        public void Test_Equals_False()
        {
            var config1 = ResourceHelper.ReadResource<BucketConfig>(@"Documents\Configs\rev94.json");
            var config2 = ResourceHelper.ReadResource<BucketConfig>(@"Documents\Configs\rev96.json");

            Assert.False(config2.Equals(config1));
        }

        [Fact]
        public void Test_Equals_NodesExt_Changed()
        {
            var config1 = ResourceHelper.ReadResource<BucketConfig>(@"Documents\Configs\rev94.json");
            var config2 = ResourceHelper.ReadResource<BucketConfig>(@"Documents\Configs\rev94.json");

            config2.NodesExt.RemoveAt(0);

            Assert.False(config2.Equals(config1));
        }

        [Fact]
        public void Test_Equals_Node_Changed()
        {
            var config1 = ResourceHelper.ReadResource<BucketConfig>(@"Documents\Configs\rev94.json");
            var config2 = ResourceHelper.ReadResource<BucketConfig>(@"Documents\Configs\rev94.json");

            config2.Nodes.RemoveAt(0);

            Assert.False(config2.Equals(config1));
        }

        [Fact]
        public void Test_Equals_Node_Value_Changed()
        {
            var config1 = ResourceHelper.ReadResource<BucketConfig>(@"Documents\Configs\rev94.json");
            var config2 = ResourceHelper.ReadResource<BucketConfig>(@"Documents\Configs\rev94.json");

            config2.Nodes[0].Hostname = "localhost:8091";

            Assert.False(config2.Equals(config1));
        }

       [Theory]
       [InlineData(true)]
       [InlineData(false)]
        public void Test_ClusterNodeChanged(bool hasChanged)
        {
            var config1 = ResourceHelper.ReadResource<BucketConfig>(@"Documents\Configs\rev94.json");
            var config2 = ResourceHelper.ReadResource<BucketConfig>(@"Documents\Configs\rev94.json");

            if (hasChanged)
            {
                config2.Nodes[0].Hostname = "localhost:8091";
                config2.Equals(config1);
                Assert.True(config2.ClusterNodesChanged);
            }
            else
            {
                config2.Equals(config1);
                Assert.False(config2.ClusterNodesChanged);
            }
        }

        [Fact]
        public void Test_Equals_NodeExt_Changed()
        {
            var config1 = ResourceHelper.ReadResource<BucketConfig>(@"Documents\Configs\rev94.json");
            var config2 = ResourceHelper.ReadResource<BucketConfig>(@"Documents\Configs\rev94.json");

            config2.NodesExt.RemoveAt(0);

            Assert.False(config2.Equals(config1));
        }

        [Fact]
        public void Test_Equals_NodeExt_Value_Changed()
        {
            var config1 = ResourceHelper.ReadResource<BucketConfig>(@"Documents\Configs\rev94.json");
            var config2 = ResourceHelper.ReadResource<BucketConfig>(@"Documents\Configs\rev94.json");

            config2.NodesExt[0].Hostname = "localhost:8091";

            Assert.False(config2.Equals(config1));
        }

        [Theory]
        [InlineData(@"Documents\Configs\rev92-single-node.json", @"Documents\Configs\rev94.json")]
        [InlineData(@"Documents\Configs\rev96.json", @"Documents\Configs\rev98-single-node.json")]
        public void Test_Filter_Removed_Nodes(string oldConfigPath, string newConfigPath)
        {
            var oldConfig = ResourceHelper.ReadResource<BucketConfig>(oldConfigPath);
            var newConfig = ResourceHelper.ReadResource<BucketConfig>(newConfigPath);

            var options = new ClusterOptions();
            var bucketNodes = new ConcurrentDictionary<IPEndPoint, IClusterNode>();
            var context = new ClusterContext(null, options);

            //load up the initial state after bootstrapping
            foreach (var server in oldConfig.NodesExt)
            {
                var endPoint = server.GetIpEndPoint(options);
                var clusterNode = new ClusterNode(context)
                {
                    EndPoint = endPoint
                };
                context.AddNode(clusterNode);
                bucketNodes.TryAdd(endPoint, clusterNode);
            }

            foreach (var nodesExt in newConfig.NodesExt)
            {
                var endPoint = nodesExt.GetIpEndPoint(options);
                if (bucketNodes.ContainsKey(endPoint))
                {
                    continue;
                }

                var clusterNode = new ClusterNode(context)
                {
                    EndPoint = endPoint
                };
                context.AddNode(clusterNode);
                bucketNodes.TryAdd(endPoint, clusterNode);
            }

            var removed = bucketNodes.Where(x =>
                !newConfig.NodesExt.Any(y => x.Key.Equals(y.GetIpEndPoint(options))));

            foreach (var valuePair in removed)
            {
                if (!bucketNodes.TryRemove(valuePair.Key, out var clusterNode)) continue;
                context.RemoveNode(clusterNode);
            }

            Assert.Equal(newConfig.NodesExt.Count, bucketNodes.Count);
            Assert.Equal(context.Nodes.Count, bucketNodes.Count);
        }
    }
}
