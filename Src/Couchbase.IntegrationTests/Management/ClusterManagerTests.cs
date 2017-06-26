using System.Linq;
using System.Threading;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.IntegrationTests.Utils;
using Couchbase.Management;
using NUnit.Framework;

namespace Couchbase.IntegrationTests.Management
{
    [TestFixture]
    public class ClusterManagerTests
    {
        private const string BucketName = "ClusterManagerTests";

        private ICluster _cluster;
        private IClusterManager _clusterManager;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _cluster = new Cluster(Utils.TestConfiguration.GetConfiguration("basic"));
            _clusterManager = _cluster.CreateManager(TestConfiguration.Settings.AdminUsername, TestConfiguration.Settings.AdminPassword);
        }

        #region bucket tests

        [Test]
        [TestCase(BucketTypeEnum.Couchbase)]
        [TestCase(BucketTypeEnum.Ephemeral), Ignore("Ephemeral buckets not supported on CI server")]
        [TestCase(BucketTypeEnum.Memcached)]
        public void CreateBucket_DoesNotExist_Success(BucketTypeEnum bucketType)
        {
            // Arrange

            // Ensure the bucket doesn't already exist
            _clusterManager.RemoveBucket(BucketName + "_" + bucketType);
            Thread.Sleep(250);

            // Act

            var result = _clusterManager.CreateBucket(BucketName, bucketType:bucketType);

            try
            {
                // Assert

                Assert.NotNull(result);
                Assert.True(result.Success);
            }
            finally
            {
                // Cleanup

                Thread.Sleep(250);

                var removeResult = _clusterManager.RemoveBucket(BucketName);
                Assert.True(removeResult.Success);

                Thread.Sleep(250);
            }
        }

        [Test]
        public void CreateBucket_Exists_Failure()
        {
            // Arrange

            // Ensure the bucket exists
            _clusterManager.CreateBucket(BucketName);
            Thread.Sleep(250);

            // Act

            var result = _clusterManager.CreateBucket(BucketName);

            try
            {
                // Assert

                Assert.NotNull(result);
                Assert.False(result.Success);
            }
            finally
            {
                // Cleanup

                Thread.Sleep(250);

                var removeResult = _clusterManager.RemoveBucket(BucketName);
                Assert.True(removeResult.Success);

                Thread.Sleep(250);
            }
        }

        [Test]
        public void CreateBucket_FlushEnableTrue_BucketFlushIsEnabled()
        {
            // Arrange

            // Ensure the bucket doesn't already exist
            _clusterManager.RemoveBucket(BucketName);
            Thread.Sleep(250);

            // Act

            var result = _clusterManager.CreateBucket(BucketName, flushEnabled: true);

            try
            {
                // Assert

                Assert.NotNull(result);
                Assert.True(result.Success);

                Thread.Sleep(250);

                var clusterInfo = _clusterManager.ClusterInfo();
                Assert.True(clusterInfo.Success);

                var bucketConfig = clusterInfo.Value.BucketConfigs().Find(p => p.Name == BucketName);
                Assert.NotNull(bucketConfig);
                Assert.That(() => !string.IsNullOrEmpty(bucketConfig.Controllers.Flush));
            }
            finally
            {
                // Cleanup

                var removeResult = _clusterManager.RemoveBucket(BucketName);
                Assert.True(removeResult.Success);

                Thread.Sleep(250);
            }
        }

        [Test]
        public void CreateBucket_FlushEnableFalse_BucketFlushIsNotEnabled()
        {
            // Arrange

            // Ensure the bucket doesn't already exist
            _clusterManager.RemoveBucket(BucketName);
            Thread.Sleep(250);

            // Act

            var result = _clusterManager.CreateBucket(BucketName, flushEnabled: false);

            try
            {
                // Assert

                Assert.NotNull(result);
                Assert.True(result.Success);

                Thread.Sleep(250);

                var clusterInfo = _clusterManager.ClusterInfo();
                Assert.True(clusterInfo.Success);

                var bucketConfig = clusterInfo.Value.BucketConfigs().Find(p => p.Name == BucketName);
                Assert.NotNull(bucketConfig);
                Assert.That(() => string.IsNullOrEmpty(bucketConfig.Controllers.Flush));
            }
            finally
            {
                // Cleanup

                var removeResult = _clusterManager.RemoveBucket(BucketName);
                Assert.True(removeResult.Success);

                Thread.Sleep(250);
            }
        }

        [Test]
        public void CreateBucket_IndexReplicasTrue_IndexReplicasAreEnabled()
        {
            // Arrange

            // Ensure the bucket doesn't already exist
            _clusterManager.RemoveBucket(BucketName);
            Thread.Sleep(250);

            // Act

            var result = _clusterManager.CreateBucket(BucketName, indexReplicas: true);

            try
            {
                // Assert

                Assert.NotNull(result);
                Assert.True(result.Success);

                Thread.Sleep(250);

                var clusterInfo = _clusterManager.ClusterInfo();
                Assert.True(clusterInfo.Success);

                var bucketConfig = clusterInfo.Value.BucketConfigs().Find(p => p.Name == BucketName);
                Assert.NotNull(bucketConfig);
                Assert.IsTrue(bucketConfig.ReplicaIndex);
            }
            finally
            {
                // Cleanup

                var removeResult = _clusterManager.RemoveBucket(BucketName);
                Assert.True(removeResult.Success);

                Thread.Sleep(250);
            }
        }

        [Test]
        public void CreateBucket_IndexReplicasFalse_IndexReplicasAreNotEnabled()
        {
            // Arrange

            // Ensure the bucket doesn't already exist
            _clusterManager.RemoveBucket(BucketName);
            Thread.Sleep(250);

            // Act

            var result = _clusterManager.CreateBucket(BucketName, indexReplicas: false);

            try
            {
                // Assert

                Assert.NotNull(result);
                Assert.True(result.Success);

                Thread.Sleep(250);

                var clusterInfo = _clusterManager.ClusterInfo();
                Assert.True(clusterInfo.Success);

                var bucketConfig = clusterInfo.Value.BucketConfigs().Find(p => p.Name == BucketName);
                Assert.NotNull(bucketConfig);
                Assert.IsFalse(bucketConfig.ReplicaIndex);
            }
            finally
            {
                // Cleanup

                var removeResult = _clusterManager.RemoveBucket(BucketName);
                Assert.True(removeResult.Success);

                Thread.Sleep(250);
            }
        }

        #endregion

        [Test, Ignore("RBAC is not yet supported in the Jenkins automated build")]
        public void Can_Upsert_List_And_Remove_User()
        {
            var user = new User
            {
                Username = "alice",
                Name = "Alice Liddell",
                Roles = new[] {new Role {Name = "fts_searcher", BucketName = "*"}}
            };

            // create user
            var createResult = _clusterManager.UpsertUser(user.Username, "secure123", user.Name, user.Roles.ToArray());
            Assert.IsTrue(createResult.Success);

            // get all users
            var getUsersResult = _clusterManager.GetUsers();
            Assert.IsTrue(getUsersResult.Success);
            Assert.IsNotNull(getUsersResult.Value.First(x => x.Username == user.Username));

            // get individual user
            var getUserResult = _clusterManager.GetUser(user.Username);
            Assert.IsTrue(getUserResult.Success);
            Assert.AreEqual(user.Username, getUserResult.Value.Username);
            Assert.AreEqual(user.Name, getUserResult.Value.Name);
            Assert.AreEqual("local", getUserResult.Value.Domain);

            // remove user
            var removeResult = _clusterManager.RemoveUser(user.Username);
            Assert.IsTrue(removeResult.Success);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _clusterManager.RemoveBucket(BucketName);
            _cluster.Dispose();
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion

