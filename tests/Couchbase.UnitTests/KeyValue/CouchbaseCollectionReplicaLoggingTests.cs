using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Compression;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.Core.Sharding;
using Couchbase.KeyValue;
using Couchbase.Management.Collections;
using Couchbase.Management.Views;
using Couchbase.UnitTests.Utils;
using Couchbase.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.KeyValue
{
    /// <summary>
    /// Regression coverage for the "no replicas configured" warning logged by
    /// <see cref="CouchbaseCollection"/>'s private VBucketForReplicas helper. The document key it logs
    /// must go through the redactor - like every other key-bearing log line in this class - rather than
    /// being emitted in the clear, and the tag it produces must match the configured
    /// <see cref="RedactionLevel"/>.
    /// </summary>
    public class CouchbaseCollectionReplicaLoggingTests
    {
        private const string DocId = "thekey";

        [Theory]
        [InlineData(RedactionLevel.None, "thekey")]
        [InlineData(RedactionLevel.Partial, "<ud>thekey</ud>")]
        [InlineData(RedactionLevel.Full, "<ud>thekey</ud>")]
        public async Task GetAllReplicas_With_No_Replicas_Redacts_Key_In_Warning(
            RedactionLevel level, string expectedIdRendering)
        {
            var loggerMock = new Mock<ILogger<CouchbaseCollection>>();
            var (collection, _) = CreateCollection(loggerMock.Object, level);

            // No replicas are configured for this vBucket, so VBucketForReplicas logs the warning
            // before dispatching to the (failing) primary. That dispatch failure is expected and is
            // not itself what this test is checking.
            var tasks = collection.GetAllReplicasAsync(DocId).ToList();
            foreach (var task in tasks)
            {
                await Assert.ThrowsAnyAsync<Exception>(() => task);
            }

            loggerMock.Verify(l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) => state.ToString().Contains($"key [{expectedIdRendering}]")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        private static (CouchbaseCollection Collection, NoReplicaFakeBucket Bucket) CreateCollection(
            ILogger<CouchbaseCollection> logger, RedactionLevel redactionLevel)
        {
            var config = ResourceHelper.ReadResource(@"Documents\Configs\configWithReplicasAndServerGroups.json",
                InternalSerializationContext.Default.BucketConfig);

            // Primary only, no replica index anywhere, so VBucket.HasReplicas is false for every key.
            config.VBucketServerMap.VBucketMap = Enumerable.Repeat<short[]>([0, -1, -1, -1], 1024).ToArray();
            config.BucketCapabilities = [BucketCapabilities.SUBDOC_REPLICA_READ];

            var bucket = new NoReplicaFakeBucket(config);

            var collection = new CouchbaseCollection(bucket,
                new OperationConfigurator(new LegacyTranscoder(),
                    Mock.Of<IOperationCompressor>(),
                    new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy()),
                    new BestEffortRetryStrategy()),
                logger,
                new Mock<ILogger<GetResult>>().Object,
                new Redactor(new TypedRedactor(redactionLevel)),
                CouchbaseCollection.DefaultCollectionName,
                Mock.Of<IScope>(scope => scope.IsDefaultScope == true && scope.Name == Scope.DefaultScopeName),
                new NoopRequestTracer(),
                NullFallbackTypeSerializerProvider.Instance,
                Mock.Of<IServiceProvider>());

            return (collection, bucket);
        }

        /// <summary>Fails every dispatched operation immediately - only the pre-dispatch warning log matters here.</summary>
        internal class NoReplicaFakeBucket : BucketBase
        {
            public NoReplicaFakeBucket(BucketConfig config)
                : base(config.Name,
                    new ClusterContext(null, new ClusterOptions().WithPasswordAuthentication("username", "password")),
                    new Mock<IScopeFactory>().Object,
                    CreateRetryOrchestrator(),
                    new Mock<ILogger>().Object,
                    new TypedRedactor(RedactionLevel.None),
                    new Mock<IBootstrapperFactory>().Object,
                    NoopRequestTracer.Instance,
                    new Mock<IOperationConfigurator>().Object,
                    new BestEffortRetryStrategy(), null)
            {
                CurrentConfig = config;
                KeyMapper = new VBucketKeyMapper(config, new VBucketServerMap(config.VBucketServerMap),
                    new VBucketFactory(new Mock<ILogger<VBucket>>().Object));
            }

            public virtual Task<ResponseStatus> Fail(IOperation operation) =>
                Task.FromException<ResponseStatus>(new TemporaryFailureException("The fake bucket serves no documents."));

            private static IRetryOrchestrator CreateRetryOrchestrator()
            {
                var mock = new Mock<IRetryOrchestrator>();

                mock.Setup(m => m.RetryAsync(It.IsAny<BucketBase>(), It.IsAny<IOperation>(),
                        It.IsAny<CancellationTokenPair>()))
                    .Returns((BucketBase bucket, IOperation op, CancellationTokenPair _) =>
                        ((NoReplicaFakeBucket) bucket).Fail(op));

                return mock.Object;
            }

            internal override Task<ResponseStatus> SendAsync(IOperation op, CancellationTokenPair token = default) =>
                Fail(op);

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
