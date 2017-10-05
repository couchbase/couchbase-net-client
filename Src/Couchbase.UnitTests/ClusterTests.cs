using Couchbase.Authentication;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Moq;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace Couchbase.UnitTests
{
    [TestFixture]
    public class ClusterTests
    {
        [Test]
        public void OpenBucket_DefaultBucket_CallsClusterController()
        {
            Mock<IClusterController> mockController = new Mock<IClusterController>();

            var cluster = new Cluster(new ClientConfiguration(), mockController.Object);
            cluster.OpenBucket();

            mockController.Verify(m => m.CreateBucket(
                It.Is<string>(a => a == "default"),
                It.Is<string>(a => a == null),
                It.Is<IAuthenticator>(a => a == null)), Times.Once());
        }

        [Test]
        public void OpenBucket_TestBucket_CallsClusterController()
        {
            Mock<IClusterController> mockController = new Mock<IClusterController>();

            var cluster = new Cluster(new ClientConfiguration(), mockController.Object);
            cluster.OpenBucket("test");

            mockController.Verify(m => m.CreateBucket(
                It.Is<string>(a => a == "test"),
                It.Is<string>(a => a == null),
                It.Is<IAuthenticator>(a => a == null)), Times.Once());
        }

        [Test]
        public void OpenBucket_TestBucketWithPassword_CallsClusterController()
        {
            Mock<IClusterController> mockController = new Mock<IClusterController>();

            var cluster = new Cluster(new ClientConfiguration(), mockController.Object);
            cluster.OpenBucket("test", "password");

            mockController.Verify(m => m.CreateBucket(
                It.Is<string>(a => a == "test"),
                It.Is<string>(a => a == "password"),
                It.Is<IAuthenticator>(a => a == null)), Times.Once());
        }

        [Test]
        public void OpenBucket_BucketNameIsNull_ThrowsException()
        {
            Mock<IClusterController> mockController = new Mock<IClusterController>();

            var cluster = new Cluster(new ClientConfiguration(), mockController.Object);

            var exception = Assert.Throws<ArgumentNullException>(() =>
            {
                cluster.OpenBucket(null);
            });

            Assert.AreEqual("bucketName", exception.ParamName);
        }

        [Test]
        public void OpenBucket_BucketNameIsNullWithPassword_ThrowsException()
        {
            Mock<IClusterController> mockController = new Mock<IClusterController>();

            var cluster = new Cluster(new ClientConfiguration(), mockController.Object);

            var exception = Assert.Throws<ArgumentNullException>(() =>
            {
                cluster.OpenBucket(null, "password");
            });

            Assert.AreEqual("bucketName", exception.ParamName);
        }


        [Test]
        public async Task OpenBucketAsync_DefaultBucket_CallsClusterController()
        {
            Mock<IClusterController> mockController = new Mock<IClusterController>();

            var cluster = new Cluster(new ClientConfiguration(), mockController.Object);
            await cluster.OpenBucketAsync();

            mockController.Verify(m => m.CreateBucketAsync(
                It.Is<string>(a => a == "default"),
                It.Is<string>(a => a == null),
                It.Is<IAuthenticator>(a => a == null)), Times.Once());
        }

        [Test]
        public async Task OpenBucketAsync_TestBucket_CallsClusterController()
        {
            Mock<IClusterController> mockController = new Mock<IClusterController>();

            var cluster = new Cluster(new ClientConfiguration(), mockController.Object);
            await cluster.OpenBucketAsync("test");

            mockController.Verify(m => m.CreateBucketAsync(
                It.Is<string>(a => a == "test"),
                It.Is<string>(a => a == null),
                It.Is<IAuthenticator>(a => a == null)), Times.Once());
        }

        [Test]
        public async Task OpenBucketAsync_TestBucketWithPassword_CallsClusterController()
        {
            Mock<IClusterController> mockController = new Mock<IClusterController>();

            var cluster = new Cluster(new ClientConfiguration(), mockController.Object);
            await cluster.OpenBucketAsync("test", "password");

            mockController.Verify(m => m.CreateBucketAsync(
                It.Is<string>(a => a == "test"),
                It.Is<string>(a => a == "password"),
                It.Is<IAuthenticator>(a => a == null)), Times.Once());
        }

        [Test]
        public void OpenBucketAsync_BucketNameIsNull_ThrowsException()
        {
            Mock<IClusterController> mockController = new Mock<IClusterController>();

            var cluster = new Cluster(new ClientConfiguration(), mockController.Object);

            var exception = Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await cluster.OpenBucketAsync(null);
            });

            Assert.AreEqual("bucketName", exception.ParamName);
        }

        [Test]
        public void OpenBucketAsync_BucketNameIsNullWithPassword_ThrowsException()
        {
            Mock<IClusterController> mockController = new Mock<IClusterController>();

            var cluster = new Cluster(new ClientConfiguration(), mockController.Object);

            var exception = Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await cluster.OpenBucketAsync(null, "password");
            });

            Assert.AreEqual("bucketName", exception.ParamName);
        }
    }
}
