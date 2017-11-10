using System;
using System.Collections.Generic;
using Couchbase.Authentication;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests
{
    [TestFixture]
    public class BucketCacheTests
    {
        [Test]
        public void Constructor_When_Configuration_Is_Null_ArgumentNullException_Is_Thrown1()
        {
            // Assert
            Assert.Throws<ArgumentNullException>(() => new BucketCache((ClientConfiguration)null));
        }

        [Test]
        public void Constructor_When_Configuration_Is_Null_ArgumentNullException_Is_Thrown2()
        {
            // Assert
            Assert.Throws<ArgumentNullException>(() => new BucketCache(null, Mock.Of<IAuthenticator>()));
        }

        [Test]
        public void Constructor_When_Authenticator_Is_Null_ArgumentNullException_Is_Thrown()
        {
            // Assert
            Assert.Throws<ArgumentNullException>(() => new BucketCache(Mock.Of<ClientConfiguration>(), null));
        }

        [Test]
        public void Constructor_When_Definition_Is_Null_ArgumentNullException_Is_Thrown()
        {
            // Assert
            Assert.Throws<ArgumentNullException>(() => new BucketCache((ICouchbaseClientDefinition)null));
        }

        [Test]
        public void Get_When_BucketName_Is_Null_ArgumentNullException_Is_Thrown()
        {
            // Arrange
            var bucketCache = new BucketCache(() => Mock.Of<ICluster>());

            // Assert
            Assert.Throws<ArgumentNullException>(() => bucketCache.Get(null));
        }

        [Test]
        public void Get_When_BucketName_Is_Empty_ArgumentException_Is_Thrown()
        {
            // Arrange
            var bucketCache = new BucketCache(() => Mock.Of<ICluster>());

            // Assert
            Assert.Throws<ArgumentException>(() => bucketCache.Get(""));
        }

        [Test]
        public void Get_Calls_OpenBucket_Using_BucketName1()
        {
            // Arrange
            var cluster = GetCluster();
            var bucketCache = new BucketCache(() => cluster);
            var bucketName = "foo";

            // Act
            bucketCache.Get(bucketName);

            // Assert
            Mock.Get(cluster)
                .Verify(_ => _.OpenBucket(bucketName));
        }

        [Test]
        public void Get_Calls_OpenBucket_Using_BucketName2()
        {
            // Arrange
            var cluster = GetCluster();
            var bucketCache = new BucketCache(() => cluster);
            var bucketName = "foo";

            // Act
            bucketCache.Get(bucketName, null);

            // Assert
            Mock.Get(cluster)
                .Verify(_ => _.OpenBucket(bucketName));
        }

        [Test]
        public void Get_Calls_OpenBucket_Using_BucketName_And_Password()
        {
            // Arrange
            var cluster = GetCluster();
            var bucketCache = new BucketCache(() => cluster);
            var bucketName = "foo";
            var password = "bar";

            // Act
            bucketCache.Get(bucketName, password);

            // Assert
            Mock.Get(cluster)
                .Verify(_ => _.OpenBucket(bucketName, password));
        }

        [Test]
        public void Get_Calls_OpenBucket_Using_Password_From_Configuration()
        {
            // Arrange
            var cluster = GetCluster();
            var bucketCache = new BucketCache(() => cluster);
            var bucketName = "foo";
            var password = "bar";

            cluster.Configuration.BucketConfigs.Add(bucketName, new BucketConfiguration() { Password = password });

            // Act
            bucketCache.Get(bucketName);

            // Assert
            Mock.Get(cluster)
                .Verify(_ => _.OpenBucket(bucketName, password));
        }

        [Test]
        public void Get_Calls_OpenBucket_Without_Password_If_Configuration_Password_Is_Null()
        {
            // Arrange
            var cluster = GetCluster();
            var bucketCache = new BucketCache(() => cluster);
            var bucketName = "foo";

            cluster.Configuration.BucketConfigs.Add(bucketName, new BucketConfiguration() { Password = null });

            // Act
            bucketCache.Get(bucketName);

            // Assert
            Mock.Get(cluster)
                .Verify(_ => _.OpenBucket(bucketName));
        }

        [Test]
        public void Get_Caches_Buckets()
        {
            // Arrange
            var cluster = GetCluster();
            var bucketCache = new BucketCache(() => cluster);
            var bucketName = "foo";

            // Act
            var bucket = bucketCache.Get(bucketName);

            // Assert

            Assert.AreSame(bucket, bucketCache.Get(bucketName));
        }

        [Test]
        public void Remove_Removes_Bucket_From_Cache()
        {
            // Arrange
            var cluster = GetCluster();
            var bucketCache = new BucketCache(() => cluster);
            var bucketName = "foo";

            var bucket = Mock.Of<IBucket>();

            Mock.Get(cluster)
                .Setup(_ => _.OpenBucket(bucketName))
                .Returns(bucket);

            bucketCache.Get(bucketName);

            // Act
            bucketCache.Remove(bucketName);

            // Assert
            bucketCache.Get(bucketName);

            Mock.Get(cluster)
                .Verify(_ => _.OpenBucket(bucketName), Times.Exactly(2));
        }

        [Test]
        public void Remove_Disposes_Removed_Bucket()
        {
            // Arrange
            var cluster = GetCluster();
            var bucketCache = new BucketCache(() => cluster);
            var bucketName = "foo";

            var bucket = Mock.Of<IBucket>();

            Mock.Get(cluster)
                .Setup(_ => _.OpenBucket(bucketName))
                .Returns(bucket);

            bucketCache.Get(bucketName);

            // Act
            bucketCache.Remove(bucketName);

            // Assert
            Mock.Get(bucket)
                .Verify(_ => _.Dispose());
        }

        [Test]
        public void Dispose_Disposes_Cached_Buckets()
        {
            // Arrange
            var cluster = GetCluster();
            var bucketCache = new BucketCache(() => cluster);
            var bucketName = "foo";

            var bucket = Mock.Of<IBucket>();

            Mock.Get(cluster)
                .Setup(_ => _.OpenBucket(bucketName))
                .Returns(bucket);

            bucketCache.Get(bucketName);

            // Act
            bucketCache.Dispose();

            // Assert
            Mock.Get(bucket)
                .Verify(_ => _.Dispose());
        }

        [Test]
        public void Dispose_Disposes_Cluster()
        {
            // Arrange
            var cluster = GetCluster();
            var bucketCache = new BucketCache(() => cluster);
            var bucketName = "foo";

            var bucket = Mock.Of<IBucket>();

            Mock.Get(cluster)
                .Setup(_ => _.OpenBucket(bucketName))
                .Returns(bucket);

            bucketCache.Get(bucketName);

            // Act
            bucketCache.Dispose();

            // Assert
            Mock.Get(cluster)
                .Verify(_ => _.Dispose());
        }

        private ICluster GetCluster()
        {
            var cluster = Mock.Of<ICluster>();
            var clientConfiguration = new ClientConfiguration();

            Mock.Get(cluster)
                .SetupGet(_ => _.Configuration)
                .Returns(clientConfiguration);

            clientConfiguration.BucketConfigs = new Dictionary<string, BucketConfiguration>();

            return cluster;
        }
    }
}
