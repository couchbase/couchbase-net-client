using System;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.Management;
using Moq;
using NUnit.Framework;

namespace Couchbase.IntegrationTests.Management
{
    [TestFixture]
    public class MemcachedBucketManagerTests
    {
        private const string BucketName = "MemcachedBucketManagerTests";
        private ICluster _cluster;
        private IClusterManager _clusterManager;
        private IBucket _bucket;
        private IBucketManager _bucketManager;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _cluster = new Cluster(Utils.TestConfiguration.GetConfiguration("basic"));
            _clusterManager = _cluster.CreateManager(ConfigurationManager.AppSettings["adminusername"],
                ConfigurationManager.AppSettings["adminpassword"]);

            var createResult = _clusterManager.CreateBucket(BucketName, flushEnabled: true, bucketType:BucketTypeEnum.Memcached);
            Console.WriteLine(createResult.Success);

            // Allow time for bucket to be created and configuration to propagate before beginning operations
            Thread.Sleep(500);

            _bucket = _cluster.OpenBucket(BucketName);
            _bucketManager = _bucket.CreateManager(ConfigurationManager.AppSettings["adminusername"],
                ConfigurationManager.AppSettings["adminpassword"]);
        }

        #region flush tests

        [Test]
        public void Flush_Success()
        {
            var result = _bucketManager.Flush();

            Assert.NotNull(result);
            Assert.True(result.Success);
        }

        [Test]
        public async Task FlushAsync_Success()
        {
            var result = await _bucketManager.FlushAsync().ConfigureAwait(false);

            Assert.NotNull(result);
            Assert.True(result.Success);
        }

        #endregion

        #region design document tests

        [Test]
        public void GetDesignDocument_Throws_NotSupportedException()
        {
              Assert.Throws<NotSupportedException>(()=> _bucketManager.GetDesignDocument("test"));
        }

        [Test]
        public void GetDesignDocuments_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucketManager.GetDesignDocuments());
        }

        [Test]
        public void InsertDesignDocument_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucketManager.InsertDesignDocument("test", It.IsAny<string>()));
        }

        [Test]
        public void UpdateDesignDocument_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucketManager.UpdateDesignDocument("test", It.IsAny<string>()));
        }

        [Test]
        public void RemoveDesignDocument_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucketManager.RemoveDesignDocument(It.IsAny<string>()));
        }

        #endregion

        #region async design document tests

        [Test]
        public void GetDesignDocumentAsync_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucketManager.GetDesignDocumentAsync(It.IsAny<string>()));
        }

        [Test]
        public void InsertDesignDocumentAsync_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucketManager.InsertDesignDocumentAsync(It.IsAny<string>(), It.IsAny<string>()));
        }

        [Test]
        public void UpdateDesignDocumentAsyn_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucketManager.UpdateDesignDocumentAsync(It.IsAny<string>(), It.IsAny<string>()));
        }

        [Test]
        public void RemoveDesignDocumentAsync_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucketManager.RemoveDesignDocumentAsync(It.IsAny<string>()));
        }

        #endregion


        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _cluster.CloseBucket(_bucket);

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
