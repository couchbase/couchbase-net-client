using System.Collections.Generic;
using System.Linq;
using Couchbase.Configuration.Server;
using Couchbase.Configuration.Server.Serialization;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests
{
    [TestFixture]
    public class ClusterInfoTests
    {
        [Test]
        public void ClusterInfo_Returns_Pools_From_ServerConfig()
        {
            const string poolName = "test_pool";

            var mockServerConfig = new Mock<IServerConfig>();
            mockServerConfig.Setup(x => x.Buckets).Returns(new List<BucketConfig>());
            mockServerConfig.Setup(x => x.Pools).Returns(new Pools
            {
                Name = poolName
            });

            var clusterInfo = new ClusterInfo(mockServerConfig.Object);

            Assert.AreEqual(0, clusterInfo.BucketConfigs().Count);
            Assert.IsNotNull(clusterInfo.Pools());
            Assert.AreEqual(poolName, clusterInfo.Pools().Name);
        }

        [Test]
        public void ClusterInfo_Returns_BucketConfigs_From_ServerConfig()
        {
            const string bucketConfigName = "test_config";

            var bucketConfigs = new List<BucketConfig>
            {
                new BucketConfig
                {
                    Name = bucketConfigName
                }
            };

            var mockServerConfig = new Mock<IServerConfig>();
            mockServerConfig.Setup(x => x.Buckets).Returns(bucketConfigs);

            var clusterInfo = new ClusterInfo(mockServerConfig.Object);

            Assert.IsNotNull(clusterInfo.BucketConfigs());
            Assert.AreEqual(bucketConfigs.Count, clusterInfo.BucketConfigs().Count);
            Assert.AreEqual(bucketConfigName, clusterInfo.BucketConfigs().First().Name);
        }
    }
}
