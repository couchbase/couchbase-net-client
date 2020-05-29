using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.CircuitBreakers;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
using Couchbase.Management.Buckets;
using Couchbase.UnitTests.Utils;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core
{
    public class ClusterContextTests
    {
        [Fact]
        public async Task PruneNodesAsync_Removes_Rebalanced_Node()
        {
            //Arrange

            var config = ResourceHelper.ReadResource<BucketConfig>(@"Documents\Configs\config-error.json");
            var context = new ClusterContext();

            var dnsResolver = new Mock<IDnsResolver>();
            var service = new IpEndPointService(dnsResolver.Object, new ClusterOptions());

            var hosts = new List<string>{"10.143.194.101", "10.143.194.102", "10.143.194.103", "10.143.194.104"};
            hosts.ForEach(async x => context.AddNode(await CreateMockedNode(x, 11210, service).ConfigureAwait(false)));

            //Act

            await context.PruneNodesAsync(config).ConfigureAwait(false);

            //Assert

            var removed = await service.GetIpEndPointAsync("10.143.194.102", 11210).ConfigureAwait(false);

            Assert.DoesNotContain(context.Nodes, node => node.EndPoint.Equals(removed));
        }

        [Fact]
        public async Task PruneNodesAsync_Does_Not_Remove_Single_Service_Nodes()
        {
            //Arrange

            var config = ResourceHelper.ReadResource<BucketConfig>(@"Documents\Configs\rev-36310-service-per-node.json");
            var context = new ClusterContext();

            var dnsResolver = new Mock<IDnsResolver>();
            var service = new IpEndPointService(dnsResolver.Object, new ClusterOptions());

            var hosts = new List<string> { "10.143.194.101", "10.143.194.102", "10.143.194.103", "10.143.194.104" };
            hosts.ForEach(async x => context.AddNode(await CreateMockedNode(x, 11210, service).ConfigureAwait(false)));

            //Act

            await context.PruneNodesAsync(config).ConfigureAwait(false);

            //Assert

            foreach (var host in hosts)
            {
                var removed = await service.GetIpEndPointAsync(host, 11210).ConfigureAwait(false);

                Assert.Contains(context.Nodes, node => node.EndPoint.Equals(removed));
            }
        }

        private async Task<IClusterNode> CreateMockedNode(string hostname, int port, IpEndPointService service)
        {
            var mockConnectionPool = new Mock<IConnectionPool>();

            var mockConnectionPoolFactory = new Mock<IConnectionPoolFactory>();
            mockConnectionPoolFactory
                .Setup(m => m.Create(It.IsAny<ClusterNode>()))
                .Returns(mockConnectionPool.Object);

            var clusterNode = new ClusterNode(new ClusterContext(), mockConnectionPoolFactory.Object,
                new Mock<ILogger<ClusterNode>>().Object, new Mock<ITypeTranscoder>().Object,
                new Mock<ICircuitBreaker>().Object,
                new Mock<ISaslMechanismFactory>().Object,
                new Mock<IRedactor>().Object,
                await service.GetIpEndPointAsync(hostname, port).ConfigureAwait(false),
                BucketType.Couchbase,
                new NodeAdapter
                {
                    Hostname = hostname,
                    KeyValue = port
                });

            return clusterNode;
        }
    }
}
