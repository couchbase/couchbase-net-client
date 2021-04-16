using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.KeyValue;
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
                CreateCollectionFactoryMock());

            // Act/Assert

            Assert.Throws<ArgumentNullException>(() => factory.CreateScope("_default", null));
        }

        [Fact]
        public void CreateDefaultScope_WithBucket_ReturnsDefaultScopeAndCollection()
        {
            // Arrange

            var factory = new ScopeFactory(new Mock<ILogger<Scope>>().Object, CreateCollectionFactoryMock());

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
            var mock = new Mock<BucketBase>(
                "default",
                new ClusterContext(),
                factory,
                new Mock<IRetryOrchestrator>().Object,
                new Mock<ILogger>().Object,
                new Mock<IRedactor>().Object,
                new Mock<IBootstrapperFactory>().Object,
                NoopRequestTracer.Instance,
                new Mock<IOperationConfigurator>().Object,
                new BestEffortRetryStrategy());

                return mock.Object;
        }

        #endregion
    }
}
