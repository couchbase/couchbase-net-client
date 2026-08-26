using System;
using System.Threading.Tasks;
using Couchbase.Client.Transactions.Support;
using Couchbase.Core;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.KeyValue;
using Couchbase.Management.Collections;
using Couchbase.Management.Views;
using Couchbase.Views;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Transactions
{
    /// <summary>
    /// The transactions layer used to pass <c>Bucket.SupportsCollections</c> as the PreserveTtl flag,
    /// with a comment calling it "a proxy for supporting TTLs". They are unrelated capabilities read
    /// from unrelated places - collections support is a bucket capability from the cluster map, while
    /// preserving expiry is a feature each connection negotiates in HELO - so whenever the two
    /// diverge, transactions either ask for something the cluster will refuse or silently skip
    /// preserving expiry on a server that supports it.
    /// </summary>
    public class PreserveTtlCapabilityTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Capability_Comes_From_The_Cluster_Not_From_Collections(bool clusterSupportsPreserveTtl)
        {
            var bucket = FakeBucket(supportsPreserveTtl: clusterSupportsPreserveTtl);
            var collection = CollectionOn(bucket);

            Assert.Equal(clusterSupportsPreserveTtl, collection.SupportsPreserveTtl());
        }

        /// <summary>
        /// The mixed-version case the old proxy got wrong: the bucket supports collections while the
        /// cluster cannot preserve expiry.
        /// </summary>
        [Fact]
        public void Collections_Support_Does_Not_Imply_PreserveTtl()
        {
            var bucket = FakeBucket(supportsPreserveTtl: false);
            var collection = CollectionOn(bucket);

            Assert.True(bucket.SupportsCollections);
            Assert.False(collection.SupportsPreserveTtl());
        }

        /// <summary>
        /// Protostellar buckets are not a <see cref="BucketBase"/> and have no cluster context to ask,
        /// so they keep the old proxy rather than silently losing TTL preservation.
        /// </summary>
        [Fact]
        public void A_bucket_without_a_cluster_context_falls_back_to_collections_support()
        {
            var bucket = Mock.Of<IBucket>(b => b.SupportsCollections == true);
            var collection = CollectionOn(bucket);

            Assert.True(collection.SupportsPreserveTtl());
        }

        private static ICouchbaseCollection CollectionOn(IBucket bucket) =>
            Mock.Of<ICouchbaseCollection>(collection =>
                collection.Scope == Mock.Of<IScope>(scope => scope.Bucket == bucket));

        private static CapabilityFakeBucket FakeBucket(bool supportsPreserveTtl)
        {
            var bucket = new CapabilityFakeBucket();
            bucket.Context.SupportsPreserveTtl = supportsPreserveTtl;
            return bucket;
        }

        /// <summary>
        /// The smallest <see cref="BucketBase"/> that can carry a cluster context. It serves no
        /// operations; the capability lookup under test never sends one.
        /// </summary>
        internal class CapabilityFakeBucket : BucketBase
        {
            public CapabilityFakeBucket()
                : base("default",
                    new ClusterContext(null,
                        new ClusterOptions().WithPasswordAuthentication("username", "password")),
                    new Mock<IScopeFactory>().Object,
                    new Mock<IRetryOrchestrator>().Object,
                    new Mock<ILogger>().Object,
                    new TypedRedactor(RedactionLevel.None),
                    new Mock<IBootstrapperFactory>().Object,
                    NoopRequestTracer.Instance,
                    new Mock<IOperationConfigurator>().Object,
                    new BestEffortRetryStrategy(), null)
            {
                CurrentConfig = new BucketConfig
                {
                    Name = "default",
                    BucketCapabilities = [BucketCapabilities.COLLECTIONS]
                };
            }

            internal override Task<ResponseStatus> SendAsync(IOperation op, CancellationTokenPair token = default) =>
                throw new NotImplementedException();

            public override ICouchbaseCollectionManager Collections => throw new NotImplementedException();

#pragma warning disable CS0618, CS0672 // Obsolete View service members
            public override IViewIndexManager ViewIndexes => throw new NotImplementedException();

            public override Task<IViewResult<TKey, TValue>> ViewQueryAsync<TKey, TValue>(string designDocument,
                string viewName, ViewOptions options = null) => throw new NotImplementedException();
#pragma warning restore CS0618, CS0672

            public override Task ForceConfigUpdateAsync() => throw new NotImplementedException();

            public override IScope Scope(string scopeName) => throw new NotImplementedException();

            internal override Task BootstrapAsync(IClusterNode bootstrapNodes) => throw new NotImplementedException();

            public override Task ConfigUpdatedAsync(BucketConfig newConfig) => throw new NotImplementedException();
        }
    }
}
