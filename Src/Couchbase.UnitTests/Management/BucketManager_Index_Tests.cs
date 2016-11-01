using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.IO.Http;
using Couchbase.Management;
using Couchbase.Management.Indexes;
using Couchbase.N1QL;
using Couchbase.Views;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests.Management
{
    [TestFixture]
    public class BucketManagerIndexTests
    {
        [Test]
        public void ListIndexes_WhenSuccessful_ReturnSuccess()
        {
            //arange
            var managerMock = new Mock<IBucketManager>();
            managerMock.Setup(x => x.ListN1qlIndexes()).Returns(new IndexResult {Success = true});

            //act
            var result = managerMock.Object.ListN1qlIndexes();

            //assert
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void ListIndexes_WhenSuccessful_ReturnsListOfIndexes()
        {
            //arange
            var managerMock = new Mock<IBucketManager>();
            managerMock.Setup(x => x.ListN1qlIndexes()).Returns(new IndexResult
            {
                Success = true,
                Value = new List<IndexInfo>
                {
                    new IndexInfo()
                }
            });

            //act
            var result = managerMock.Object.ListN1qlIndexes();

            //assert
            Assert.AreEqual(1, result.Value.Count);
        }

        [Test]
        public void ListIndexes_WhenFailed_ReturnsEmptyList()
        {
            //arange
            var managerMock = new Mock<IBucketManager>();
            managerMock.Setup(x => x.ListN1qlIndexes()).Returns(new IndexResult
            {
                Success = false,
                Value = new List<IndexInfo>()
            });

            //act
            var result = managerMock.Object.ListN1qlIndexes();

            //assert
            Assert.AreEqual(0, result.Value.Count);
        }

        [Test]
        public void DropIndex_WhenNamed_ReturnsValidStatement()
        {
            //arange
            var mockBucket = new Mock<IBucket>();
            mockBucket.Setup(x => x.Name).Returns("beer-sample");
            var bucketManager = new TestableBucketManager(mockBucket.Object);
            var expectedStatement = "DROP INDEX `beer-sample`.`theName` USING GSI;";

            //act
            var result = (DefaultResult<string>)bucketManager.DropN1qlIndex("theName");

            //assert
            Assert.AreEqual(expectedStatement, result.Value);
        }

        [Test]
        public async Task DropIndexAsync_WhenNamed_ReturnsValidStatement()
        {
            //arange
            var mockBucket = new Mock<IBucket>();
            mockBucket.Setup(x => x.Name).Returns("beer-sample");
            var bucketManager = new TestableBucketManager(mockBucket.Object);
            var expectedStatement = "DROP INDEX `beer-sample`.`theName` USING GSI;";

            //act
            var result = (DefaultResult<string>) await bucketManager.DropN1qlIndexAsync("theName");

            //assert
            Assert.AreEqual(expectedStatement, result.Value);
        }

        [Test]
        public void CreateIndex_WhenNamed_ReturnsValidStatement()
        {
            //arange
            var mockBucket = new Mock<IBucket>();
            mockBucket.Setup(x => x.Name).Returns("beer-sample");
            var bucketManager = new TestableBucketManager(mockBucket.Object);
            var expectedStatement = "CREATE INDEX `theName` ON `beer-sample`(`name`, `id`) USING GSI WITH {\"defer_build\":false};";

            //act
            var result = (DefaultResult<string>)bucketManager.CreateN1qlIndex("theName", false, "name", "id");

            //assert
            Assert.AreEqual(expectedStatement, result.Value);
        }

        [Test]
        public void CreatePrimaryIndex_WithDeferFalse_ReturnsValidStatement()
        {
            //arange
            var mockBucket = new Mock<IBucket>();
            mockBucket.Setup(x => x.Name).Returns("beer-sample");
            var bucketManager = new TestableBucketManager(mockBucket.Object);
            var expectedStatement = "CREATE PRIMARY INDEX ON `beer-sample` USING GSI WITH {\"defer_build\":false};";

            //act
            var result = (DefaultResult<string>) bucketManager.CreateN1qlPrimaryIndex();

            //assert
            Assert.AreEqual(expectedStatement, result.Value);
        }

        [Test]
        public void CreatePrimaryIndex_WithDeferTrue_ReturnsValidStatement()
        {
            //arange
            var mockBucket = new Mock<IBucket>();
            mockBucket.Setup(x => x.Name).Returns("beer-sample");
            var bucketManager = new TestableBucketManager(mockBucket.Object);
            var expectedStatement = "CREATE PRIMARY INDEX ON `beer-sample` USING GSI WITH {\"defer_build\":true};";

            //act
            var result = (DefaultResult<string>)bucketManager.CreateN1qlPrimaryIndex(true);

            //assert
            Assert.AreEqual(expectedStatement, result.Value);
        }

        [Test]
        public async Task CreateNamedPrimaryIndexAsync_WithDeferFalse_ReturnsValidStatement()
        {
            //arange
            var mockBucket = new Mock<IBucket>();
            mockBucket.Setup(x => x.Name).Returns("beer-sample");
            var bucketManager = new TestableBucketManager(mockBucket.Object);
            var expectedStatement = "CREATE PRIMARY INDEX `my_idx` ON `beer-sample` USING GSI WITH {\"defer_build\":false};";

            //act
            var result = (DefaultResult<string>) await bucketManager.CreateN1qlPrimaryIndexAsync("my_idx");

            //assert
            Assert.AreEqual(expectedStatement, result.Value);
        }

        [Test]
        public void CreateNamedPrimaryIndex_WithDeferFalse_ReturnsValidStatement()
        {
            //arange
            var mockBucket = new Mock<IBucket>();
            mockBucket.Setup(x => x.Name).Returns("beer-sample");
            var bucketManager = new TestableBucketManager(mockBucket.Object);
            var expectedStatement = "CREATE PRIMARY INDEX `my_idx` ON `beer-sample` USING GSI WITH {\"defer_build\":false};";

            //act
            var result = (DefaultResult<string>)bucketManager.CreateN1qlPrimaryIndex("my_idx");

            //assert
            Assert.AreEqual(expectedStatement, result.Value);
        }

        [Test]
        public async Task CreatePrimaryIndexAsync_WithDeferFalse_ReturnsValidStatement()
        {
            //arange
            var mockBucket = new Mock<IBucket>();
            mockBucket.Setup(x => x.Name).Returns("beer-sample");
            var bucketManager = new TestableBucketManager(mockBucket.Object);
            var expectedStatement = "CREATE PRIMARY INDEX ON `beer-sample` USING GSI WITH {\"defer_build\":false};";

            //act
            var result = (DefaultResult<string>) await bucketManager.CreateN1qlPrimaryIndexAsync();

            //assert
            Assert.AreEqual(expectedStatement, result.Value);
        }

        [Test]
        public async Task CreatePrimaryIndexAsync_WithDeferTrue_ReturnsValidStatement()
        {
            //arange
            var mockBucket = new Mock<IBucket>();
            mockBucket.Setup(x => x.Name).Returns("beer-sample");
            var bucketManager = new TestableBucketManager(mockBucket.Object);
            var expectedStatement = "CREATE PRIMARY INDEX ON `beer-sample` USING GSI WITH {\"defer_build\":true};";

            //act
            var result = (DefaultResult<string>) await bucketManager.CreateN1qlPrimaryIndexAsync(true);

            //assert
            Assert.AreEqual(expectedStatement, result.Value);
        }

        [Test]
        public void CreateIndex_WhenNamedAndDefer_ReturnsValidStatement()
        {
            //arange
            var mockBucket = new Mock<IBucket>();
            mockBucket.Setup(x => x.Name).Returns("beer-sample");
            var bucketManager = new TestableBucketManager(mockBucket.Object);
            var expectedStatement = "CREATE INDEX `theName` ON `beer-sample`(`name`) USING GSI WITH {\"defer_build\":true};";

            //act
            var result = (DefaultResult<string>)bucketManager.CreateN1qlIndex("theName", true, "name");

            //assert
            Assert.AreEqual(expectedStatement, result.Value);
        }

        [Test]
        public async Task CreateIndexAsync_WhenNamedAndDefer_ReturnsValidStatement()
        {
            //arange
            var mockBucket = new Mock<IBucket>();
            mockBucket.Setup(x => x.Name).Returns("beer-sample");
            var bucketManager = new TestableBucketManager(mockBucket.Object);
            var expectedStatement = "CREATE INDEX `theName` ON `beer-sample`(`name`) USING GSI WITH {\"defer_build\":true};";

            //act
            var result = (DefaultResult<string>) await bucketManager.CreateN1qlIndexAsync("theName", true, "name");

            //assert
            Assert.AreEqual(expectedStatement, result.Value);
        }

        [Test]
        public void CreateIndex_WhenNamedAndManyFields_ReturnsValidStatement()
        {
            //arange
            var mockBucket = new Mock<IBucket>();
            mockBucket.Setup(x => x.Name).Returns("beer-sample");
            var bucketManager = new TestableBucketManager(mockBucket.Object);
            var expectedStatement = "CREATE INDEX `theName` ON `beer-sample`(`name`, `id`) USING GSI WITH {\"defer_build\":false};";

            //act
            var result = (DefaultResult<string>)bucketManager.CreateN1qlIndex("theName", false, "name", "id");

            //assert
            Assert.AreEqual(expectedStatement, result.Value);
        }

        [Test]
        public async Task CreateIndexAsync_WhenNamedAndManyFields_ReturnsValidStatement()
        {
            //arange
            var mockBucket = new Mock<IBucket>();
            mockBucket.Setup(x => x.Name).Returns("beer-sample");
            var bucketManager = new TestableBucketManager(mockBucket.Object);
            var expectedStatement = "CREATE INDEX `theName` ON `beer-sample`(`name`, `id`) USING GSI WITH {\"defer_build\":false};";

            //act
            var result = (DefaultResult<string>) await bucketManager.CreateN1qlIndexAsync("theName", false, "name", "id");

            //assert
            Assert.AreEqual(expectedStatement, result.Value);
        }

        [Test]
        public void DropNamedPrimaryIndex_WhenNamedAndManyFields_ReturnsValidStatement()
        {
            //arange
            var mockBucket = new Mock<IBucket>();
            mockBucket.Setup(x => x.Name).Returns("beer-sample");
            var bucketManager = new TestableBucketManager(mockBucket.Object);
            var expectedStatement = "DROP INDEX `beer-sample`.`theName` USING GSI;";

            //act
            var result = (DefaultResult<string>) bucketManager.DropN1qlPrimaryIndex("theName");

            //assert
            Assert.AreEqual(expectedStatement, result.Value);
        }

        [Test]
        public async Task DropNamedPrimaryIndexAsync_WhenNamedAndManyFields_ReturnsValidStatement()
        {
            //arange
            var mockBucket = new Mock<IBucket>();
            mockBucket.Setup(x => x.Name).Returns("beer-sample");
            var bucketManager = new TestableBucketManager(mockBucket.Object);
            var expectedStatement = "DROP INDEX `beer-sample`.`theName` USING GSI;";

            //act
            var result = (DefaultResult<string>) await bucketManager.DropNamedPrimaryIndexAsync("theName");

            //assert
            Assert.AreEqual(expectedStatement, result.Value);
        }

        [Test]
        public void DropPrimaryIndex_ReturnsValidStatement()
        {
            //arange
            var mockBucket = new Mock<IBucket>();
            mockBucket.Setup(x => x.Name).Returns("beer-sample");
            var bucketManager = new TestableBucketManager(mockBucket.Object);
            var expectedStatement = "DROP PRIMARY INDEX ON `beer-sample` USING GSI;";

            //act
            var result = (DefaultResult<string>)bucketManager.DropN1qlPrimaryIndex();

            //assert
            Assert.AreEqual(expectedStatement, result.Value);
        }

        [Test]
        public async Task DropPrimaryIndexAsync_ReturnsValidStatement()
        {
            //arange
            var mockBucket = new Mock<IBucket>();
            mockBucket.Setup(x => x.Name).Returns("beer-sample");
            var bucketManager = new TestableBucketManager(mockBucket.Object);
            var expectedStatement = "DROP PRIMARY INDEX ON `beer-sample` USING GSI;";

            //act
            var result = await bucketManager.DropN1qlPrimaryIndexAsync();

            //assert
            Assert.AreEqual(expectedStatement, ((DefaultResult<string>)result).Value);
        }

        [Test]
        public void WatchIndexes_Retries_Until_Indexes_Are_Online()
        {
            var attempts = 0;

            var mockBucket = new Mock<IBucket>();
            mockBucket.Setup(x => x.Name).Returns("default");
            mockBucket.Setup(x => x.Query<IndexInfo>(It.IsAny<QueryRequest>()))
                .Returns(() => new QueryResult<IndexInfo>
                {
                    Success = true,
                    Rows = new List<IndexInfo>
                    {
                        new IndexInfo {Name = "foo", State = attempts++ < 2 ? "pending" : "online"}
                    }
                });

            var indexNamesToWatch = new List<string> { "foo" };

            var manager = new TestableBucketManager(mockBucket.Object);
            var result = manager.WatchN1qlIndexes(indexNamesToWatch, TimeSpan.MaxValue);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.Value.Count);
            Assert.AreEqual("foo", result.Value.First().Name);
            Assert.AreEqual("online", result.Value.First().State);

            mockBucket.Verify(x => x.Query<IndexInfo>(It.IsAny<QueryRequest>()), Times.Exactly(3));
        }

        [Test]
        public void WatchIndexes_Returns_When_Query_Failed()
        {
            var mockBucket = new Mock<IBucket>();
            mockBucket.Setup(x => x.Name).Returns("default");
            mockBucket.Setup(x => x.Query<IndexInfo>(It.IsAny<QueryRequest>()))
                .Returns(new QueryResult<IndexInfo>
                {
                    Success = false
                });

            var indexNamesToWatch = new List<string> { "foo" };

            var manager = new TestableBucketManager(mockBucket.Object);
            var result = manager.WatchN1qlIndexes(indexNamesToWatch, TimeSpan.FromSeconds(20));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(0, result.Value.Count);

            mockBucket.Verify(x => x.Query<IndexInfo>(It.IsAny<QueryRequest>()), Times.Exactly(1));
        }

        [Test]
        public void WatchIndexes_Returns_If_No_Matching_Indexes_Found()
        {
            var mockBucket = new Mock<IBucket>();
            mockBucket.Setup(x => x.Name).Returns("default");
            mockBucket.Setup(x => x.Query<IndexInfo>(It.IsAny<QueryRequest>()))
                .Returns(new QueryResult<IndexInfo>
                {
                    Success = true,
                    Rows = new List<IndexInfo>
                    {
                        new IndexInfo {Name = "bar", State = "pending"}
                    }
                });

            var indexNamesToWatch = new List<string> { "foo" };

            var manager = new TestableBucketManager(mockBucket.Object);
            var result = manager.WatchN1qlIndexes(indexNamesToWatch, TimeSpan.FromSeconds(1));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.Value.Count);
            Assert.AreEqual("bar", result.Value.First().Name);
            Assert.AreEqual("pending", result.Value.First().State);

            mockBucket.Verify(x => x.Query<IndexInfo>(It.IsAny<QueryRequest>()), Times.Exactly(1));
        }

        [Test]
        public void WatchIndexes_Retries_Until_Timeout_And_Returns_Last_Result()
        {
            var mockBucket = new Mock<IBucket>();
            mockBucket.Setup(x => x.Name).Returns("default");
            mockBucket.Setup(x => x.Query<IndexInfo>(It.IsAny<QueryRequest>()))
                .Returns(() => new QueryResult<IndexInfo>
                {
                    Success = true,
                    Rows = new List<IndexInfo>
                    {
                        new IndexInfo {Name = "foo", State = "pending"}
                    }
                });

            var indexNamesToWatch = new List<string> { "foo" };

            var manager = new TestableBucketManager(mockBucket.Object);
            var result = manager.WatchN1qlIndexes(indexNamesToWatch, TimeSpan.FromSeconds(1));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.Value.Count);
            Assert.AreEqual("foo", result.Value.First().Name);
            Assert.AreEqual("pending", result.Value.First().State);

            mockBucket.Verify(x => x.Query<IndexInfo>(It.IsAny<QueryRequest>()), Times.AtLeastOnce);
        }

        [Test]
        public async Task WatchIndexesAsync_Retries_Until_Indexes_Are_Online()
        {
            var attempts = 0;

            var mockBucket = new Mock<IBucket>();
            mockBucket.Setup(x => x.Name).Returns("default");
            mockBucket.Setup(x => x.QueryAsync<IndexInfo>(It.IsAny<QueryRequest>()))
                .Returns(() => Task.FromResult((IQueryResult<IndexInfo>) new QueryResult<IndexInfo>
                {
                    Success = true,
                    Rows = new List<IndexInfo>
                    {
                        new IndexInfo {Name = "foo", State = attempts++ < 2 ? "pending" : "online"}
                    }
                }));

            var indexNamesToWatch = new List<string> { "foo" };

            var manager = new TestableBucketManager(mockBucket.Object);
            var result = await manager.WatchN1qlIndexesAsync(indexNamesToWatch, TimeSpan.FromSeconds(20));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.Value.Count);
            Assert.AreEqual("foo", result.Value.First().Name);
            Assert.AreEqual("online", result.Value.First().State);

            mockBucket.Verify(x => x.QueryAsync<IndexInfo>(It.IsAny<QueryRequest>()), Times.Exactly(3));
        }

        [Test]
        public async Task WatchIndexesAsync_Returns_When_Qeury_Failed()
        {
            var mockBucket = new Mock<IBucket>();
            mockBucket.Setup(x => x.Name).Returns("default");
            mockBucket.Setup(x => x.QueryAsync<IndexInfo>(It.IsAny<QueryRequest>()))
                .ReturnsAsync(new QueryResult<IndexInfo>
                {
                    Success = false
                });

            var indexNamesToWatch = new List<string> { "foo" };

            var manager = new TestableBucketManager(mockBucket.Object);
            var result = await manager.WatchN1qlIndexesAsync(indexNamesToWatch, TimeSpan.FromSeconds(20));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(0, result.Value.Count);

            mockBucket.Verify(x => x.QueryAsync<IndexInfo>(It.IsAny<QueryRequest>()), Times.Exactly(1));
        }

        [Test]
        public async Task WatchIndexesAsync_Returns_If_No_Matching_Indexes_Found()
        {
            var mockBucket = new Mock<IBucket>();
            mockBucket.Setup(x => x.Name).Returns("default");
            mockBucket.Setup(x => x.QueryAsync<IndexInfo>(It.IsAny<QueryRequest>()))
                .ReturnsAsync(new QueryResult<IndexInfo>
                {
                    Success = true,
                    Rows = new List<IndexInfo>
                    {
                        new IndexInfo {Name = "bar", State = "pending"}
                    }
                });

            var indexNamesToWatch = new List<string> { "foo" };

            var manager = new TestableBucketManager(mockBucket.Object);
            var result = await manager.WatchN1qlIndexesAsync(indexNamesToWatch, TimeSpan.FromSeconds(1));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.Value.Count);
            Assert.AreEqual("bar", result.Value.First().Name);
            Assert.AreEqual("pending", result.Value.First().State);

            mockBucket.Verify(x => x.QueryAsync<IndexInfo>(It.IsAny<QueryRequest>()), Times.Exactly(1));
        }

        [Test]
        public async Task WatchIndexesAsync_Retries_Until_Timeout_And_Returns_Last_Result()
        {
            var mockBucket = new Mock<IBucket>();
            mockBucket.Setup(x => x.Name).Returns("default");
            mockBucket.Setup(x => x.QueryAsync<IndexInfo>(It.IsAny<QueryRequest>()))
                .ReturnsAsync(new QueryResult<IndexInfo>
                {
                    Success = true,
                    Rows = new List<IndexInfo>
                    {
                        new IndexInfo {Name = "foo", State = "pending"}
                    }
                });

            var indexNamesToWatch = new List<string> { "foo" };

            var manager = new TestableBucketManager(mockBucket.Object);
            var result = await manager.WatchN1qlIndexesAsync(indexNamesToWatch, TimeSpan.FromSeconds(1));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.Value.Count);
            Assert.AreEqual("foo", result.Value.First().Name);
            Assert.AreEqual("pending", result.Value.First().State);

            mockBucket.Verify(x => x.QueryAsync<IndexInfo>(It.IsAny<QueryRequest>()), Times.AtLeastOnce);
        }
    }

    public class TestableBucketManager : BucketManager
    {
        public TestableBucketManager(IBucket bucket, ClientConfiguration clientConfig,
            IDataMapper mapper, HttpClient httpClient, string username, string password)
            : base(bucket, clientConfig, mapper, httpClient, username, password)
        {
        }

        public TestableBucketManager(IBucket bucket) : base(bucket, null, null,  null, null, null)
        {
        }

        protected override Task<IResult> ExecuteIndexRequestAsync(string statement)
        {
            return Task.FromResult((IResult) new DefaultResult<string>(true, "", null)
            {
                Value = statement
            });
        }

        protected override IResult ExecuteIndexRequest(string statement)
        {
            return new DefaultResult<string>(true, "", null)
            {
                Value = statement
            };
        }
    }
}