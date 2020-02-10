using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
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
        #region CreateScopes

        [Fact]
        public void CreateScopes_NullBucket_ArgumentNullException()
        {
            // Arrange

            var factory = new ScopeFactory(new Mock<ILogger<Scope>>().Object,
                CreateCollectionFactoryMock());

            // Act/Assert

            Assert.Throws<ArgumentNullException>(() => factory.CreateScopes(null, new Manifest()).ToList());
        }

        [Fact]
        public void CreateScopes_NullManifest_ArgumentNullException()
        {
            // Arrange

            var factory = new ScopeFactory(new Mock<ILogger<Scope>>().Object,
                CreateCollectionFactoryMock());

            // Act/Assert

            Assert.Throws<ArgumentNullException>(() => factory.CreateScopes(CreateBucketMock(factory), null).ToList());
        }

        [Fact]
        public void CreateDefaultScopes_WithManifest_ReturnsScopesAndCollections()
        {
            // Arrange

            var factory = new ScopeFactory(new Mock<ILogger<Scope>>().Object,
                CreateCollectionFactoryMock());

            var manifest = new Manifest
            {
                scopes = new List<ScopeDef>
                {
                    new ScopeDef
                    {
                        name = "scope1",
                        uid = "100",
                        collections = new List<CollectionDef>
                        {
                            new CollectionDef {name = "collection1", uid = "1"},
                            new CollectionDef {name = "collection2", uid = "2"}
                        }
                    },
                    new ScopeDef
                    {
                        name = "scope2",
                        uid = "200",
                        collections = new List<CollectionDef>
                        {
                            new CollectionDef {name = "collection3", uid = "fe"},
                            new CollectionDef {name = "collection4", uid = "ff"}
                        }
                    }
                }
            };

            // Act

            var result = factory.CreateScopes(CreateBucketMock(factory), manifest).ToList();

            // Assert

            var scope1 = result.First(p => p.Name == "scope1");
            Assert.Equal("100", scope1.Id);

            var collection1 = scope1["collection1"];
            Assert.Equal("collection1", collection1.Name);
            Assert.Equal((uint?) 1, collection1.Cid);

            var collection2 = scope1["collection2"];
            Assert.Equal("collection2", collection2.Name);
            Assert.Equal((uint?) 2, collection2.Cid);

            var scope2 = result.First(p => p.Name == "scope2");
            Assert.Equal("100", scope1.Id);

            var collection3 = scope2["collection3"];
            Assert.Equal("collection3", collection3.Name);
            Assert.Equal((uint?) 0xfe, collection3.Cid);

            var collection4 = scope2["collection4"];
            Assert.Equal("collection4", collection4.Name);
            Assert.Equal((uint?) 0xff, collection4.Cid);
        }

        #endregion

        #region CreateDefaultScope

        [Fact]
        public void CreateDefaultScope_NullBucket_ArgumentNullException()
        {
            // Arrange

            var factory = new ScopeFactory(new Mock<ILogger<Scope>>().Object,
                CreateCollectionFactoryMock());

            // Act/Assert

            Assert.Throws<ArgumentNullException>(() => factory.CreateDefaultScope(null));
        }

        [Fact]
        public void CreateDefaultScope_WithBucket_ReturnsDefaultScopeAndCollection()
        {
            // Arrange

            var factory = new ScopeFactory(new Mock<ILogger<Scope>>().Object,
                CreateCollectionFactoryMock());

            // Act

            var result = factory.CreateDefaultScope(CreateBucketMock(factory));

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
                    m.Create(It.IsAny<BucketBase>(), It.IsAny<uint?>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns((BucketBase bucket, uint? cid, string name, string scopeName) =>
                {
                    var collection = new Mock<ICollection>();
                    collection.SetupGet(m => m.Name).Returns(name);
                    collection.SetupGet(m => m.Cid).Returns(cid);

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
                new Mock<IRedactor>().Object);

            return mock.Object;
        }

        #endregion
    }
}
