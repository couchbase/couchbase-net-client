using System;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Diagnostics;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Microsoft.Extensions.Logging;
using Moq;
using Couchbase.Management.Collections;
using Xunit;
using Couchbase.Core.Configuration.Server;

namespace Couchbase.UnitTests
{
    public class CouchbaseBucketTests
    {
        [Fact]
        public async Task Scope_DoesNotThrow_ScopeNoteFoundException()
        {
            var bucket = new CouchbaseBucket("default",
                new ClusterContext(null, new ClusterOptions().WithPasswordAuthentication("username", "password"))
                {
                    SupportsCollections = true
                },
                new Mock<IScopeFactory>().Object,
                new Mock<IRetryOrchestrator>().Object,
                new Mock<IVBucketKeyMapperFactory>().Object,
                new Mock<ILogger<CouchbaseBucket>>().Object,
                new TypedRedactor(RedactionLevel.None),
                new Mock<IBootstrapperFactory>().Object,
                NoopRequestTracer.Instance,
                new Mock<IOperationConfigurator>().Object,
                new BestEffortRetryStrategy(),
                new BucketConfig(),
                new Mock<IConfigPushHandlerFactory>().Object);

            bucket.Scope("doesnotexist");
            await bucket.ScopeAsync("doesnotexist");
        }

        [Fact]
        public async Task WaitUntilReadyAsync_Offline_DesiredState_Throws_InvalidArgumentException()
        {
            var bucket = CreateBucket();

            await Assert.ThrowsAsync<InvalidArgumentException>(() => bucket.WaitUntilReadyAsync(TimeSpan.FromSeconds(1),
                new WaitUntilReadyOptions().DesiredState(ClusterState.Offline)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task WaitUntilReadyAsync_NonPositive_Timeout_Throws_UnambiguousTimeoutException(int seconds)
        {
            var bucket = CreateBucket();

            await Assert.ThrowsAsync<UnambiguousTimeoutException>(() =>
                bucket.WaitUntilReadyAsync(TimeSpan.FromSeconds(seconds)));
        }

        [Fact]
        public async Task WaitUntilReadyAsync_Without_A_Config_Or_Nodes_Times_Out()
        {
            var bucket = CreateBucket(config: null);

            await Assert.ThrowsAsync<UnambiguousTimeoutException>(() =>
                bucket.WaitUntilReadyAsync(TimeSpan.FromMilliseconds(100)));
        }

        private static CouchbaseBucket CreateBucket() => CreateBucket(new BucketConfig());

        private static CouchbaseBucket CreateBucket(BucketConfig config) =>
            new("default",
                new ClusterContext(null, new ClusterOptions().WithPasswordAuthentication("username", "password")),
                new Mock<IScopeFactory>().Object,
                new Mock<IRetryOrchestrator>().Object,
                new Mock<IVBucketKeyMapperFactory>().Object,
                new Mock<ILogger<CouchbaseBucket>>().Object,
                new TypedRedactor(RedactionLevel.None),
                new Mock<IBootstrapperFactory>().Object,
                NoopRequestTracer.Instance,
                new Mock<IOperationConfigurator>().Object,
                new BestEffortRetryStrategy(),
                config,
                new Mock<IConfigPushHandlerFactory>().Object);
    }
}
