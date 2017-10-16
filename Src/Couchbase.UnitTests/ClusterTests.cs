using System;
using System.Security.Authentication;
using System.Threading.Tasks;
using Couchbase.Authentication;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.N1QL;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests
{
    [TestFixture]
    public class ClusterTests
    {
        [Test]
        public void When_Bucket_Is_Open_IsOpen_Returns_True()
        {
            var config = new ClientConfiguration();
            var mockClusterController = new Mock<IClusterController>();
            mockClusterController.Setup(x => x.IsObserving("default")).Returns(true);

            var cluster = new Cluster(config, mockClusterController.Object);
            Assert.IsTrue(cluster.IsOpen("default"));
        }

        [Test]
        public void When_Bucket_Is_Not_Open_IsOpen_Returns_False()
        {
            var config = new ClientConfiguration();
            var mockClusterController = new Mock<IClusterController>();
            mockClusterController.Setup(x => x.IsObserving("default")).Returns(false);

            var cluster = new Cluster(config, mockClusterController.Object);
            Assert.IsFalse(cluster.IsOpen("default"));
        }

        [Test]
        public async Task QueryAsync_With_QueryReqyest_Calls_RequestExecutor()
        {
            const string statement = "SELECT * FROM `default`;";
            var mockBucket = new Mock<IBucket>();
            mockBucket.Setup(x => x.QueryAsync<dynamic>(It.IsAny<QueryRequest>()))
                .ReturnsAsync(new QueryResult<dynamic>
                {
                    Success = true
                });

            var mockController = new Mock<IClusterController>();
            mockController.Setup(x => x.GetBucket(It.IsAny<IAuthenticator>())).Returns(mockBucket.Object);

            var configuration = new ClientConfiguration();
            configuration.SetAuthenticator(new PasswordAuthenticator("username", "password"));

            var cluster = new Cluster(configuration, mockController.Object);
            var result = await cluster.QueryAsync<dynamic>(statement);

            Assert.IsTrue(result.Success);

            mockController.Verify(x => x.GetBucket(It.IsAny<IAuthenticator>()), Times.Once);
            mockBucket.Verify(x => x.QueryAsync<dynamic>(It.Is<QueryRequest>(req => req.GetOriginalStatement() == statement)), Times.Once);
        }

        [Test]
        public async Task QueryAsync_With_Statement_String_Calls_RequestExecutor()
        {
            const string statement = "SELECT * FROM `default`;";
            var mockBucket = new Mock<IBucket>();
            mockBucket.Setup(x => x.QueryAsync<dynamic>(It.IsAny<QueryRequest>()))
                .ReturnsAsync(new QueryResult<dynamic>
                {
                    Success = true
                });

            var mockController = new Mock<IClusterController>();
            mockController.Setup(x => x.GetBucket(It.IsAny<IAuthenticator>())).Returns(mockBucket.Object);

            var configuration = new ClientConfiguration();
            configuration.SetAuthenticator(new PasswordAuthenticator("username", "password"));

            var cluster = new Cluster(configuration, mockController.Object);
            var request = new QueryRequest(statement);
            var result = await cluster.QueryAsync<dynamic>(request);

            Assert.IsTrue(result.Success);

            mockController.Verify(x => x.GetBucket(It.IsAny<IAuthenticator>()), Times.Once);
            mockBucket.Verify(x => x.QueryAsync<dynamic>(It.Is<QueryRequest>(req => req.GetOriginalStatement() == statement)), Times.Once);
        }

        [Test]
        public void OpenBucket_With_Null_bucketName_Throws_ArguementNullException()
        {
            var cluster = new Cluster();
            Assert.Throws<ArgumentNullException>(() => cluster.OpenBucket(null));
        }

        [Test]
        public void OpenBucket_With_Empty_bucketName_Throws_ArguementException()
        {
            var cluster = new Cluster();
            Assert.Throws<ArgumentException>(() => cluster.OpenBucket(string.Empty));
        }

        [Test]
        public void CreateManager_Throws_AithenticatorException_When_Authenticator_Is_Null()
        {
            var cluster = new Cluster();
            Assert.Throws<AuthenticationException>(() => cluster.CreateManager());
        }
    }
}
