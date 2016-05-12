using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Core;
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
        public async void DropIndexAsync_WhenNamed_ReturnsValidStatement()
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
        public async void CreateNamedPrimaryIndexAsync_WithDeferFalse_ReturnsValidStatement()
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
        public async void CreatePrimaryIndexAsync_WithDeferFalse_ReturnsValidStatement()
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
        public async void CreatePrimaryIndexAsync_WithDeferTrue_ReturnsValidStatement()
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
        public async void CreateIndexAsync_WhenNamedAndDefer_ReturnsValidStatement()
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
        public async void CreateIndexAsync_WhenNamedAndManyFields_ReturnsValidStatement()
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
        public async void DropNamedPrimaryIndexAsync_WhenNamedAndManyFields_ReturnsValidStatement()
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
        public async void DropPrimaryIndexAsync_ReturnsValidStatement()
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
    }

    public class TestableBucketManager : BucketManager
    {
        public TestableBucketManager(IBucket bucket, ClientConfiguration clientConfig, HttpClient httpClient,
            IDataMapper mapper, string username, string password)
            : base(bucket, clientConfig, httpClient, mapper, username, password)
        {
        }

        public TestableBucketManager(IBucket bucket) : base(bucket, null, null, null, null, null)
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