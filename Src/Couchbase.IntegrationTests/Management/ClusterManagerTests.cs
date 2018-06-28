using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.Core.Version;
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
            _cluster.SetupEnhancedAuth();
            _clusterManager = _cluster.CreateManager(TestConfiguration.Settings.AdminUsername, TestConfiguration.Settings.AdminPassword);
        }

        #region bucket tests

        [Test]
        [TestCase(BucketTypeEnum.Couchbase)]
        [TestCase(BucketTypeEnum.Ephemeral)]
        [TestCase(BucketTypeEnum.Memcached)]
        public void CreateBucket_DoesNotExist_Success(BucketTypeEnum bucketType)
        {
            if (!TestConfiguration.Settings.EnhancedAuth && bucketType == BucketTypeEnum.Ephemeral)
            {
                Assert.Ignore("Emphemeral buckets require CB server 5.0+");
            }

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

        [Test]
        public void Can_Upsert_List_And_Remove_User()
        {
            if (!TestConfiguration.Settings.EnhancedAuth)
            {
                Assert.Ignore("User Managment requires CB server 5.0+");
            }

            var user = new User
            {
                Username = "alice",
                Name = "Alice Liddell",
                Roles = new[] {new Role {Name = "fts_searcher", BucketName = "*"}}
            };

            // create user
            var createResult = _clusterManager.UpsertUser(AuthenticationDomain.Local, user.Username, "secure123", user.Name, user.Roles.ToArray());
            Assert.IsTrue(createResult.Success);

            // get all users
            var getUsersResult = _clusterManager.GetUsers(AuthenticationDomain.Local);
            Assert.IsTrue(getUsersResult.Success);
            Assert.IsNotNull(getUsersResult.Value.First(x => x.Username == user.Username));

            // get individual user
            var getUserResult = _clusterManager.GetUser(AuthenticationDomain.Local, user.Username);
            Assert.IsTrue(getUserResult.Success);
            Assert.AreEqual(user.Username, getUserResult.Value.Username);
            Assert.AreEqual(user.Name, getUserResult.Value.Name);
            Assert.AreEqual("local", getUserResult.Value.Domain);

            // remove user
            var removeResult = _clusterManager.RemoveUser(AuthenticationDomain.Local, user.Username);
            Assert.IsTrue(removeResult.Success);
        }

        #region FTS Index Management

        [Test]
        public async Task Can_List_All_FTS_Indexes()
        {
            var result = await _clusterManager.GetAllSearchIndexDefinitionsAsync(CancellationToken.None);
            Assert.IsTrue(result.Success);
        }

        [Test]
        public async Task Can_Create_Get_and_Delete_FTS_Index()
        {
            var definition = new SearchIndexDefinition("test-index", "default");

            try
            {
                var createResult = await _clusterManager.CreateSearchIndexAsync(definition);
                Assert.IsTrue(createResult.Success);
                Assert.IsNull(createResult.Value);

                var getResult = await _clusterManager.GetSearchIndexDefinitionAsync(definition.IndexName);
                Assert.IsTrue(getResult.Success);
                Assert.IsNotEmpty(getResult.Value);

                // wait a second to give the index to register
                await Task.Delay(TimeSpan.FromSeconds(1));

                var countResult = await _clusterManager.GetSearchIndexDocumentCountAsync(definition.IndexName);
                Assert.IsTrue(countResult.Success);
                Assert.IsTrue(countResult.Value > 0);
            }
            finally
            {
                var deleteResult = await _clusterManager.DeleteSearchIndexAsync(definition.IndexName);
                Assert.IsTrue(deleteResult.Success);
            }
        }

        [TestCase(SearchIndexIngestionMode.Pause)]
        [TestCase(SearchIndexIngestionMode.Resume)]
        public async Task Can_Set_SearchIndex_IngestionMode(SearchIndexIngestionMode mode)
        {
            if (ClusterVersionProvider.Instance.GetVersion(_cluster) < new ClusterVersion(new Version(5, 0)))
            {
                Assert.Ignore("Requires Couchbase Server 5 or greater");
            }

            var definition = new SearchIndexDefinition("ingestion-index", "default");

            try
            {
                var createResult = await _clusterManager.CreateSearchIndexAsync(definition);
                Assert.IsTrue(createResult.Success);
                Assert.IsNull(createResult.Value);

                var pauseIngestionResult = await _clusterManager.SetSearchIndexIngestionModeAsync(definition.IndexName, mode);
                Assert.IsTrue(pauseIngestionResult.Success);
            }
            finally
            {
                var deleteResult = await _clusterManager.DeleteSearchIndexAsync(definition.IndexName);
                Assert.IsTrue(deleteResult.Success);
            }
        }

        [TestCase(SearchIndexQueryMode.Allow)]
        [TestCase(SearchIndexQueryMode.Disallow)]
        public async Task Can_Set_SearchIndex_QueryMode(SearchIndexQueryMode mode)
        {
            if (ClusterVersionProvider.Instance.GetVersion(_cluster) < new ClusterVersion(new Version(5, 0)))
            {
                Assert.Ignore("Requires Couchbase Server 5 or greater");
            }

            var definition = new SearchIndexDefinition("index-index", "default");

            try
            {
                var createResult = await _clusterManager.CreateSearchIndexAsync(definition);
                Assert.IsTrue(createResult.Success);
                Assert.IsNull(createResult.Value);

                var pauseIngestionResult = await _clusterManager.SetSearchIndexQueryModeAsync(definition.IndexName, mode);
                Assert.IsTrue(pauseIngestionResult.Success);
            }
            finally
            {
                var deleteResult = await _clusterManager.DeleteSearchIndexAsync(definition.IndexName);
                Assert.IsTrue(deleteResult.Success);
            }
        }

        [TestCase(SearchIndexPlanFreezeMode.Freeze)]
        [TestCase(SearchIndexPlanFreezeMode.Unfreeze)]
        public async Task Can_Set_SearchIndex_PlanFreezeMode(SearchIndexPlanFreezeMode mode)
        {
            if (ClusterVersionProvider.Instance.GetVersion(_cluster) < new ClusterVersion(new Version(5, 0)))
            {
                Assert.Ignore("Requires Couchbase Server 5 or greater");
            }

            var definition = new SearchIndexDefinition("plan-index", "default");

            try
            {
                var createResult = await _clusterManager.CreateSearchIndexAsync(definition);
                Assert.IsTrue(createResult.Success);
                Assert.IsNull(createResult.Value);

                var pauseIngestionResult = await _clusterManager.SetSearchIndexPlanModeAsync(definition.IndexName, mode);
                Assert.IsTrue(pauseIngestionResult.Success);
            }
            finally
            {
                var deleteResult = await _clusterManager.DeleteSearchIndexAsync(definition.IndexName);
                Assert.IsTrue(deleteResult.Success);
            }
        }

        [Test]
        public async Task Can_Get_All_SearchIndex_Statistics()
        {
            var getResult = await _clusterManager.GetSearchIndexStatisticsAsync();
            Assert.IsTrue(getResult.Success);
            Assert.IsNotEmpty(getResult.Value);
        }

        [Test]
        public async Task Can_Get_SearchIndex_Statistics()
        {
            var definition = new SearchIndexDefinition("statistics-index", "default");

            try
            {
                var createResult = await _clusterManager.CreateSearchIndexAsync(definition);
                Assert.IsTrue(createResult.Success);
                Assert.IsNull(createResult.Value);

                // give the index a little time to build before trying to get statistics
                await Task.Delay(500);

                var getResult = await _clusterManager.GetSearchIndexStatisticsAsync(definition.IndexName);
                Assert.IsTrue(getResult.Success);
                Assert.IsNotEmpty(getResult.Value);
            }
            finally
            {
                var deleteResult = await _clusterManager.DeleteSearchIndexAsync(definition.IndexName);
                Assert.IsTrue(deleteResult.Success);
            }
        }

        [Test]
        public async Task Can_Get_All_SearchIndex_Partition_Information()
        {
            var getResult = await _clusterManager.GetAllSearchIndexPartitionInfoAsync();
            Assert.IsTrue(getResult.Success);
            Assert.IsNotEmpty(getResult.Value);
        }

        [Test, Ignore("Requires partitions in search index")]
        public async Task Can_Get_SearchIndex_Partition_Information()
        {
            var definition = new SearchIndexDefinition("partition-index", "default");

            try
            {
                var createResult = await _clusterManager.CreateSearchIndexAsync(definition);
                Assert.IsTrue(createResult.Success);
                Assert.IsNull(createResult.Value);

                var getResult = await _clusterManager.GetSearchIndexPartitionInfoAsync(definition.IndexName);
                Assert.IsTrue(getResult.Success);
                Assert.IsNotEmpty(getResult.Value);
            }
            finally
            {
                var deleteResult = await _clusterManager.DeleteSearchIndexAsync(definition.IndexName);
                Assert.IsTrue(deleteResult.Success);
            }
        }

        [Test, Ignore("Requires partitions in search index")]
        public async Task Can_Get_SearchIndex_Partition_Count()
        {
            var definition = new SearchIndexDefinition("partition-index", "default");

            try
            {
                var createResult = await _clusterManager.CreateSearchIndexAsync(definition);
                Assert.IsTrue(createResult.Success);
                Assert.IsNull(createResult.Value);

                var getResult = await _clusterManager.GetSearchIndexPartitionDocumentCountAsync(definition.IndexName);
                Assert.IsTrue(getResult.Success);
                Assert.IsInstanceOf<int>(getResult.Value);
            }
            finally
            {
                var deleteResult = await _clusterManager.DeleteSearchIndexAsync(definition.IndexName);
                Assert.IsTrue(deleteResult.Success);
            }
        }

        #endregion

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
