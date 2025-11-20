using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

namespace Couchbase.UnitTests.Core.DI
{
    public class ScopeFactoryTests
    {
        #region CreateDefaultScope

        [Fact]
        public void CreateDefaultScope_NullBucket_ArgumentNullException()
        {
            // Arrange

            var factory = new ScopeFactory(new Mock<ILogger<Scope>>().Object,
                CreateCollectionFactoryMock(), new Mock<IEventingFunctionManagerFactory>().Object);

            // Act/Assert

            Assert.Throws<ArgumentNullException>(() => factory.CreateScope("_default", null));
        }

        [Fact]
        public void CreateDefaultScope_WithBucket_ReturnsDefaultScopeAndCollection()
        {
            // Arrange

            var factory = new ScopeFactory(new Mock<ILogger<Scope>>().Object, CreateCollectionFactoryMock(), new Mock<IEventingFunctionManagerFactory>().Object);

            // Act

            var result = factory.CreateScope("_default", CreateBucketMock(factory));

            // Assert

            Assert.NotNull(result);
            Assert.Equal(Scope.DefaultScopeName, result.Name);

            var collection = result[CouchbaseCollection.DefaultCollectionName];
            Assert.NotNull(collection);
            Assert.Equal(CouchbaseCollection.DefaultCollectionName, collection.Name);
        }

        #endregion

        #region Helpers

        private static ICollectionFactory CreateCollectionFactoryMock()
        {
            var mock = new Mock<ICollectionFactory>();
            mock
                .Setup(m =>
                    m.Create(It.IsAny<BucketBase>(), It.IsAny<IScope>(), It.IsAny<string>()))
                .Returns((BucketBase bucket, IScope scope, string name) =>
                {
                    var collection = new Mock<ICouchbaseCollection>();
                    collection.SetupGet(m => m.Name).Returns(name);
                    collection.SetupGet(m => m.Scope).Returns(scope);

                    return collection.Object;
                });

            return mock.Object;
        }

        private static BucketBase CreateBucketMock(IScopeFactory factory)
        {
            var mockCluster = new Mock<ICluster>();
            var mock = new Mock<BucketBase>(
                "default",
                new ClusterContext(mockCluster.Object, new CancellationTokenSource(), new ClusterOptions().WithPasswordAuthentication("username", "password")),
                factory,
                new Mock<IRetryOrchestrator>().Object,
                new Mock<ILogger>().Object,
                new TypedRedactor(RedactionLevel.None),
                new Mock<IBootstrapperFactory>().Object,
                NoopRequestTracer.Instance,
                new Mock<IOperationConfigurator>().Object,
                new BestEffortRetryStrategy(),
                new BucketConfig());

            mock.SetupGet(it => it.Name).Returns("default");
            return mock.Object;
        }

        #endregion
    }
}
