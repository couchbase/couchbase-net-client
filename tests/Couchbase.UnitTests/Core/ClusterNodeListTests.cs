using Couchbase.Core;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core;

public class ClusterNodeListTests
{
    [Fact]
    public void Test_Remove()
    {
        var node1 = new Mock<IClusterNode>();
        node1.Setup(x => x.BucketName).Returns("default");
        node1.Setup(x => x.EndPoint).Returns(new HostEndpointWithPort("127.0.0.1", 10210));

        var node2 = new Mock<IClusterNode>();
        node2.Setup(x => x.BucketName).Returns("default2");
        node2.Setup(x => x.EndPoint).Returns(new HostEndpointWithPort("127.0.0.1", 10210));

        var clusterNodeList = new ClusterNodeList
        {
            node1.Object,
            node2.Object
        };

        clusterNodeList.Remove(node2.Object);

        Assert.Equal(1, clusterNodeList.Count);
    }
}
