using Couchbase.Core.Configuration.Server;
using Couchbase.UnitTests.Utils;
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

            config2.Nodes[0].hostname = "localhost:8091";

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
                config2.Nodes[0].hostname = "localhost:8091";
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

            config2.NodesExt[0].hostname = "localhost:8091";

            Assert.False(config2.Equals(config1));
        }
    }
}
