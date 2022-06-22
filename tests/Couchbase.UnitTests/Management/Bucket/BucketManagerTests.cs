using Xunit;
using Couchbase.Core;
using Moq;

namespace Couchbase.UnitTests.Management.Bucket
{
    public class BucketManagerTests
    {
        [Fact]
        public void When_NotConnected_BucketManager_Throws_NodeUnavailableException()
        {
            var clusterContext = new ClusterContext();
            var serviceUriProviderMock = new Mock<ServiceUriProvider>(clusterContext);

            var serviceUriProvider = serviceUriProviderMock.Object;
            Assert.Throws<ServiceNotAvailableException>(()=>serviceUriProvider.GetRandomManagementUri());
        }
    }
}
