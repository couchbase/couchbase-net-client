using System;
using System.Configuration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Management;
using Couchbase.Utils;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.IntegrationTests.Management
{
    [TestFixture]
    public class ClusterManagerTests
    {
        private const string BucketName = "ClusterManagerTests";

        private ICluster _cluster;
        private IClusterManager _clusterManager;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            _cluster = new Cluster(Utils.TestConfiguration.GetConfiguration("basic"));
            _clusterManager = _cluster.CreateManager(ConfigurationManager.AppSettings["adminusername"],
                ConfigurationManager.AppSettings["adminpassword"]);
        }

        #region bucket tests

        [Test]
        public void CreateBucket_DoesNotExist_Success()
        {
            // Arrange

            // Ensure the bucket doesn't already exist
            _clusterManager.RemoveBucket(BucketName);
            Thread.Sleep(250);

            // Act

            var result = _clusterManager.CreateBucket(BucketName);

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
                Assert.IsNotNullOrEmpty(bucketConfig.Controllers.Flush);
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
                Assert.IsNullOrEmpty(bucketConfig.Controllers.Flush);
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

        [TestFixtureTearDown]
        public void TestFixtureTearDown()
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

