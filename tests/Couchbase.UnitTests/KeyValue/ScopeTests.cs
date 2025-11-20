using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.KeyValue;
using Couchbase.Management.Eventing.Internal;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.KeyValue
{
    public class ScopeTests
    {
        [Theory]
        [InlineData("travel-sample", "inventory", "default:`travel-sample`.`inventory`")]
        [InlineData("`travel-sample`", "inventory", "default:`travel-sample`.`inventory`")]
        [InlineData("travel-sample", "`inventory`", "default:`travel-sample`.`inventory`")]
        [InlineData("`travel-sample`", "`inventory`", "default:`travel-sample`.`inventory`")]
        [InlineData("travel-sample`", "inventory", "default:`travel-sample`.`inventory`")]
        [InlineData("travel-sample`", "inventory`", "default:`travel-sample`.`inventory`")]
        [InlineData("`travel-sample`", "`inventory", "default:`travel-sample`.`inventory`")]
        [InlineData("travel-sample`", "`inventory`", "default:`travel-sample`.`inventory`")]
        public void Test_QueryContext(string bucketName, string scopeName, string expectedContext)
        {
            var mockCluster = new Mock<ICluster>();
            ICluster cluster = mockCluster.Object;
            var mockClusterContext = new Mock<ClusterContext>(
                cluster,
                new CancellationTokenSource(),
                new ClusterOptions().WithPasswordAuthentication("username", "password"));
            var context = new ClusterContext(cluster, new CancellationTokenSource(), new ClusterOptions().WithPasswordAuthentication("username", "password"));
            var bucket = new Mock<BucketBase>(
                bucketName,
                mockClusterContext.Object,
                new Mock<IScopeFactory>().Object,
                new Mock<IRetryOrchestrator>().Object,
                new Mock<ILogger>().Object,
                new TypedRedactor(RedactionLevel.None),
                new Mock<IBootstrapperFactory>().Object,
                NoopRequestTracer.Instance,
                new Mock<IOperationConfigurator>().Object,
                new BestEffortRetryStrategy(),
                new BucketConfig());
            bucket.Setup(x => x.Name).Returns(bucketName);

            var scope = new Scope(scopeName, bucket.Object, new Mock<ICollectionFactory>().Object,
                new Mock<ILogger<Scope>>().Object, new Mock<IEventingFunctionManagerFactory>().Object);

            Assert.Equal(expectedContext, scope.QueryContext);
        }
    }
}
