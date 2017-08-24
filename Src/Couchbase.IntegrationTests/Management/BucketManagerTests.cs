using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.IntegrationTests.Utils;
using Couchbase.Management;
using Couchbase.Utils;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.IntegrationTests.Management
{
    [TestFixture]
    public class BucketManagerTests
    {
        private const string BucketName = "BucketManagerTests";

        private static readonly object DesignDoc = new
        {
            views = new
            {
                testView = new
                {
                    map =
                        @"function (doc, meta) {
                            emit(doc.type, null);
                        }"
                }
            }
        };

        private ICluster _cluster;
        private IClusterManager _clusterManager;
        private IBucket _bucket;
        private IBucketManager _bucketManager;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _cluster = new Cluster(Utils.TestConfiguration.GetConfiguration("basic"));
            _cluster.SetupEnhancedAuth();
            _clusterManager = _cluster.CreateManager(TestConfiguration.Settings.AdminUsername, TestConfiguration.Settings.AdminPassword);

            var listbucketsResult = _clusterManager.ListBuckets();
            if (listbucketsResult.Value.Any(bucket => bucket.Name == BucketName))
            {
                var removeResult =_clusterManager.RemoveBucket(BucketName);
                Assert.IsTrue(removeResult.Success);
            }

            var createResult = _clusterManager.CreateBucket(BucketName, replicaNumber:ReplicaNumber.Zero, flushEnabled: true);
            Assert.True(createResult.Success);

            // Allow time for bucket to be created and configuration to propagate before beginning operations
            Thread.Sleep(500);

            _bucket = _cluster.OpenBucket(BucketName);
            _bucketManager = _bucket.CreateManager(TestConfiguration.Settings.AdminUsername, TestConfiguration.Settings.AdminPassword);
        }

        #region design document tests

        [Test]
        public void GetDesignDocument_Exists_Success()
        {
            // Arrange

            var createResult = _bucketManager.InsertDesignDocument("test", JsonConvert.SerializeObject(DesignDoc));
            Assert.True(createResult.Success);

            try
            {
                // Act

                var result = _bucketManager.GetDesignDocument("test");

                // Assert

                Assert.NotNull(result);
                Assert.True(result.Success, "{0}-{1}", result.Message,
                    result.Exception == null ? "" : result.Exception.ToString());
            }
            finally
            {
                // Cleanup

                var removeResult = _bucketManager.RemoveDesignDocument("test");
                Assert.True(removeResult.Success, "{0}-{1}", removeResult.Message,
                    removeResult.Exception == null ? "" : removeResult.Exception.ToString());
            }
        }

        [Test]
        public void GetDesignDocument_DoesNotExist_Failure()
        {
            // Arrange

            // ensure that the design document doesn't exist (ignore failure)
            _bucketManager.RemoveDesignDocument("test");

            // Act

            var result = _bucketManager.GetDesignDocument("test");

            // Assert

            Assert.NotNull(result);
            Assert.False(result.Success, "{0}-{1}", result.Message,
                result.Exception == null ? "" : result.Exception.ToString());
        }

        [Test]
        public void GetDesignDocuments_Success()
        {
            var result = _bucketManager.GetDesignDocuments();

            Assert.NotNull(result);
            Assert.True(result.Success, "{0}-{1}", result.Message,
                result.Exception == null ? "" : result.Exception.ToString());
        }

        [Test]
        public void InsertDesignDocument_DoesNotExist_Success()
        {
            // Arrange

            // ensure that the design document doesn't exist (ignore failure)
            _bucketManager.RemoveDesignDocument("test");

            // Act

            var result = _bucketManager.InsertDesignDocument("test", JsonConvert.SerializeObject(DesignDoc));

            // Assert

            Assert.NotNull(result);
            Assert.True(result.Success);

            // Cleanup

            var removeResult = _bucketManager.RemoveDesignDocument("test");
            Assert.True(removeResult.Success, "{0}-{1}", result.Message,
                result.Exception == null ? "" : result.Exception.ToString());
        }

        [Test]
        public void InsertDesignDocument_Exists_Success()
        {
            // Arrange

            var createResult = _bucketManager.InsertDesignDocument("test", JsonConvert.SerializeObject(DesignDoc));
            Assert.True(createResult.Success);

            try
            {
                // Act

                var result = _bucketManager.InsertDesignDocument("test", JsonConvert.SerializeObject(DesignDoc));

                // Assert

                Assert.NotNull(result);
                Assert.True(result.Success);
            }
            finally
            {
                // Cleanup

                var removeResult = _bucketManager.RemoveDesignDocument("test");
                Assert.True(removeResult.Success, "{0}-{1}", removeResult.Message,
                    removeResult.Exception == null ? "" : removeResult.Exception.ToString());
            }
        }

        [Test]
        public void UpdateDesignDocument_DoesNotExist_Success()
        {
            // Arrange

            // ensure that the design document doesn't exist (ignore failure)
            _bucketManager.RemoveDesignDocument("test");

            // Act

            var result = _bucketManager.UpdateDesignDocument("test", JsonConvert.SerializeObject(DesignDoc));

            // Assert

            Assert.NotNull(result);
            Assert.True(result.Success, "{0}-{1}", result.Message,
                result.Exception == null ? "" : result.Exception.ToString());

            // Cleanup

            var removeResult = _bucketManager.RemoveDesignDocument("test");
            Assert.True(removeResult.Success, "{0}-{1}", removeResult.Message,
                removeResult.Exception == null ? "" : removeResult.Exception.ToString());
        }

        [Test]
        public void UpdateDesignDocument_Exists_Success()
        {
            // Arrange

            var createResult = _bucketManager.InsertDesignDocument("test", JsonConvert.SerializeObject(DesignDoc));
            Assert.True(createResult.Success);

            try
            {

                // Act

                var result = _bucketManager.UpdateDesignDocument("test", JsonConvert.SerializeObject(DesignDoc));

                // Assert

                Assert.NotNull(result);
                Assert.True(result.Success, "{0}-{1}", result.Message,
                    result.Exception == null ? "" : result.Exception.ToString());
            }
            finally
            {
                // Cleanup

                var removeResult = _bucketManager.RemoveDesignDocument("test");
                Assert.True(removeResult.Success, "{0}-{1}", removeResult.Message,
                    removeResult.Exception == null ? "" : removeResult.Exception.ToString());
            }
        }

        [Test]
        public void RemoveDesignDocument_DoesNotExist_Failure()
        {
            // Arrange

            // ensure that the design document doesn't exist (ignore failure)
            _bucketManager.RemoveDesignDocument("test");

            // Act

            var result = _bucketManager.RemoveDesignDocument("test");

            // Assert

            Assert.NotNull(result);
            Assert.False(result.Success, "{0}-{1}", result.Message,
                result.Exception == null ? "" : result.Exception.ToString());
        }

        [Test]
        public void RemoveDesignDocument_Exists_Success()
        {
            // Arrange

            var createResult = _bucketManager.InsertDesignDocument("test", JsonConvert.SerializeObject(DesignDoc));
            Assert.True(createResult.Success);

            // Act

            var result = _bucketManager.RemoveDesignDocument("test");

            // Assert

            Assert.NotNull(result);
            Assert.True(result.Success, "{0}-{1}", result.Message,
                result.Exception == null ? "" : result.Exception.ToString());
        }

        #endregion

        #region async design document tests

        [Test]
        public async Task GetDesignDocumentAsync_Exists_Success()
        {
            // Arrange

            var createResult = await _bucketManager.InsertDesignDocumentAsync("test", JsonConvert.SerializeObject(DesignDoc));
            Assert.True(createResult.Success, "{0}-{1}", createResult.Message,
                createResult.Exception == null ? "" : createResult.Exception.ToString());

            try
            {
                // Act

                var result = await _bucketManager.GetDesignDocumentAsync("test");

                // Assert

                Assert.NotNull(result);
                Assert.True(result.Success, "{0}-{1}", result.Message,
                    result.Exception == null ? "" : result.Exception.ToString());
            }
            finally
            {
                // Cleanup
                var removeResult =  _bucketManager.RemoveDesignDocumentAsync("test").Result;
                Assert.True(removeResult.Success, "{0}-{1}",removeResult.Message,
                    removeResult.Exception == null ? "" : removeResult.Exception.ToString());
            }
        }

        [Test]
        public async Task GetDesignDocumentAsync_DoesNotExist_Failure()
        {
            // Arrange

            // ensure that the design document doesn't exist (ignore failure)
            await _bucketManager.RemoveDesignDocumentAsync("test");

            // Act

            var result = await _bucketManager.GetDesignDocumentAsync("test");

            // Assert

            Assert.NotNull(result);
            Assert.False(result.Success, "{0}-{1}", result.Message,
                result.Exception == null ? "" : result.Exception.ToString());
        }

        [Test]
        public async Task GetDesignDocumentsAsync_Success()
        {
            var result = await _bucketManager.GetDesignDocumentsAsync();

            Assert.NotNull(result);
            Assert.True(result.Success, "{0}-{1}", result.Message,
                result.Exception == null ? "" : result.Exception.ToString());
        }

        [Test]
        public async Task InsertDesignDocumentAsync_DoesNotExist_Success()
        {
            // Arrange

            // ensure that the design document doesn't exist (ignore failure)
            await _bucketManager.RemoveDesignDocumentAsync("test");

            // Act

            var result = await _bucketManager.InsertDesignDocumentAsync("test", JsonConvert.SerializeObject(DesignDoc));

            // Assert

            Assert.NotNull(result);
            Assert.True(result.Success, "{0}-{1}", result.Message,
                result.Exception == null ? "" : result.Exception.ToString());

            // Cleanup

            var removeResult = await _bucketManager.RemoveDesignDocumentAsync("test");
            Assert.True(removeResult.Success);
        }

        [Test]
        public async Task InsertDesignDocumentAsync_Exists_Success()
        {
            // Arrange

            var createResult = await _bucketManager.InsertDesignDocumentAsync("test", JsonConvert.SerializeObject(DesignDoc));
            Assert.True(createResult.Success, "{0}-{1}", createResult.Message,
                createResult.Exception == null ? "" : createResult.Exception.ToString());

            try
            {
                // Act

                var result = await _bucketManager.InsertDesignDocumentAsync("test", JsonConvert.SerializeObject(DesignDoc));

                // Assert

                Assert.NotNull(result);
                Assert.True(result.Success, "{0}-{1}", result.Message,
                    result.Exception == null ? "" : result.Exception.ToString());
            }
            finally
            {
                // Cleanup
                var removeResult = _bucketManager.RemoveDesignDocumentAsync("test").Result;
                Assert.True(removeResult.Success, "{0}-{1}", removeResult.Message,
                    removeResult.Exception == null ? "" : removeResult.Exception.ToString());
            }
        }

        [Test]
        public async Task UpdateDesignDocumentAsync_DoesNotExist_Success()
        {
            // Arrange

            // ensure that the design document doesn't exist (ignore failure)
            await _bucketManager.RemoveDesignDocumentAsync("test");

            // Act

            var result = await _bucketManager.UpdateDesignDocumentAsync("test", JsonConvert.SerializeObject(DesignDoc));

            // Assert

            Assert.NotNull(result);
            Assert.True(result.Success, "{0}-{1}", result.Message,
                result.Exception == null ? "" : result.Exception.ToString());

            // Cleanup

            var removeResult = await _bucketManager.RemoveDesignDocumentAsync("test");
            Assert.True(removeResult.Success, "{0}-{1}", removeResult.Message,
                removeResult.Exception == null ? "" : removeResult.Exception.ToString());
        }

        [Test]
        public async Task UpdateDesignDocumentAsync_Exists_Success()
        {
            // Arrange

            var createResult = await _bucketManager.InsertDesignDocumentAsync("test", JsonConvert.SerializeObject(DesignDoc));
            Assert.True(createResult.Success, "{0}-{1}", createResult.Message,
                createResult.Exception == null ? "" : createResult.Exception.ToString());

            try
            {

                // Act

                var result = await _bucketManager.UpdateDesignDocumentAsync("test", JsonConvert.SerializeObject(DesignDoc));

                // Assert

                Assert.NotNull(result);
                Assert.True(result.Success, "{0}-{1}", result.Message,
                    result.Exception == null ? "" : result.Exception.ToString());
            }
            finally
            {
                // Cleanup

                var removeResult = _bucketManager.RemoveDesignDocumentAsync("test").Result;
                Assert.True(removeResult.Success, "{0}-{1}", removeResult.Message,
                    removeResult.Exception == null ? "" : removeResult.Exception.ToString());
            }
        }

        [Test]
        public async Task RemoveDesignDocumentAsync_DoesNotExist_Failure()
        {
            // Arrange

            // ensure that the design document doesn't exist (ignore failure)
            await _bucketManager.RemoveDesignDocumentAsync("test");

            // Act

            var result = await _bucketManager.RemoveDesignDocumentAsync("test");

            // Assert

            Assert.NotNull(result);
            Assert.False(result.Success, "{0}-{1}", result.Message,
                result.Exception == null ? "" : result.Exception.ToString());
        }

        [Test]
        public async Task RemoveDesignDocumentAsync_Exists_Success()
        {
            // Arrange

            var createResult = await _bucketManager.InsertDesignDocumentAsync("test", JsonConvert.SerializeObject(DesignDoc));
            Assert.True(createResult.Success);

            // Act

            var result = await _bucketManager.RemoveDesignDocumentAsync("test");

            // Assert

            Assert.NotNull(result);
            Assert.True(result.Success, "{0}-{1}", result.Message,
                result.Exception == null ? "" : result.Exception.ToString());
        }

        #endregion

        #region flush tests

        [Test]
        public void Flush_Success()
        {
            var result = _bucketManager.Flush();

            Assert.NotNull(result);
            Assert.True(result.Success, "{0}-{1}", result.Message,
                result.Exception == null ? "" : result.Exception.ToString());
        }

        [Test]
        public async Task FlushAsync_Success()
        {
            var result = await _bucketManager.FlushAsync();

            Assert.NotNull(result);
            Assert.True(result.Success, "{0}-{1}", result.Message,
                result.Exception == null ? "" : result.Exception.ToString());
        }

        #endregion

        #region deadlock tests

        [Test]
        public void GetDesignDocument_NoDeadlock()
        {
            // Using an asynchronous operation within an MVC Web API action causes
            // a deadlock if you wait for the result synchronously.

            var context = new Mock<SynchronizationContext>
            {
                CallBase = true
            };

            SynchronizationContext.SetSynchronizationContext(context.Object);
            try
            {
                _bucketManager.GetDesignDocument("test");

                // If request is incorrectly awaiting on the current SynchronizationContext
                // We will see calls to Post or Send on the mock

                context.Verify(m => m.Post(It.IsAny<SendOrPostCallback>(), It.IsAny<object>()), Times.Never);
                context.Verify(m => m.Send(It.IsAny<SendOrPostCallback>(), It.IsAny<object>()), Times.Never);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        [Test]
        public async Task GetDesignDocumentAsync_NoDeadlock()
        {
            // Using an asynchronous operation within an MVC Web API action causes
            // a deadlock if you wait for the result synchronously.

            var context = new Mock<SynchronizationContext>
            {
                CallBase = true
            };

            SynchronizationContext.SetSynchronizationContext(context.Object);
            try
            {
                await _bucketManager.GetDesignDocumentAsync("test").ContinueOnAnyContext();

                // If request is incorrectly awaiting on the current SynchronizationContext
                // We will see calls to Post or Send on the mock

                context.Verify(m => m.Post(It.IsAny<SendOrPostCallback>(), It.IsAny<object>()), Times.Never);
                context.Verify(m => m.Send(It.IsAny<SendOrPostCallback>(), It.IsAny<object>()), Times.Never);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        [Test]
        public void GetDesignDocuments_NoDeadlock()
        {
            // Using an asynchronous operation within an MVC Web API action causes
            // a deadlock if you wait for the result synchronously.

            var context = new Mock<SynchronizationContext>
            {
                CallBase = true
            };

            SynchronizationContext.SetSynchronizationContext(context.Object);
            try
            {
                _bucketManager.GetDesignDocuments();

                // If request is incorrectly awaiting on the current SynchronizationContext
                // We will see calls to Post or Send on the mock

                context.Verify(m => m.Post(It.IsAny<SendOrPostCallback>(), It.IsAny<object>()), Times.Never);
                context.Verify(m => m.Send(It.IsAny<SendOrPostCallback>(), It.IsAny<object>()), Times.Never);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        [Test]
        public async Task GetDesignDocumentsAsync_NoDeadlock()
        {
            // Using an asynchronous operation within an MVC Web API action causes
            // a deadlock if you wait for the result synchronously.

            var context = new Mock<SynchronizationContext>
            {
                CallBase = true
            };

            SynchronizationContext.SetSynchronizationContext(context.Object);
            try
            {
                await _bucketManager.GetDesignDocumentsAsync().ContinueOnAnyContext();

                // If request is incorrectly awaiting on the current SynchronizationContext
                // We will see calls to Post or Send on the mock

                context.Verify(m => m.Post(It.IsAny<SendOrPostCallback>(), It.IsAny<object>()), Times.Never);
                context.Verify(m => m.Send(It.IsAny<SendOrPostCallback>(), It.IsAny<object>()), Times.Never);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        [Test]
        public void InsertUpdateAndRemoveDesignDocument_NoDeadlock()
        {
            // Using an asynchronous operation within an MVC Web API action causes
            // a deadlock if you wait for the result synchronously.

            var context = new Mock<SynchronizationContext>
            {
                CallBase = true
            };

            SynchronizationContext.SetSynchronizationContext(context.Object);
            try
            {
                _bucketManager.InsertDesignDocument("test", JsonConvert.SerializeObject(DesignDoc));
                _bucketManager.UpdateDesignDocument("test", JsonConvert.SerializeObject(DesignDoc));
                _bucketManager.RemoveDesignDocument("test");

                // If request is incorrectly awaiting on the current SynchronizationContext
                // We will see calls to Post or Send on the mock

                context.Verify(m => m.Post(It.IsAny<SendOrPostCallback>(), It.IsAny<object>()), Times.Never);
                context.Verify(m => m.Send(It.IsAny<SendOrPostCallback>(), It.IsAny<object>()), Times.Never);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        [Test]
        public async Task InsertUpdateAndRemoveDesignDocumentAsync_NoDeadlock()
        {
            // Using an asynchronous operation within an MVC Web API action causes
            // a deadlock if you wait for the result synchronously.

            var context = new Mock<SynchronizationContext>
            {
                CallBase = true
            };

            SynchronizationContext.SetSynchronizationContext(context.Object);
            try
            {
                await _bucketManager.InsertDesignDocumentAsync("test", JsonConvert.SerializeObject(DesignDoc)).ContinueOnAnyContext();
                await _bucketManager.UpdateDesignDocumentAsync("test", JsonConvert.SerializeObject(DesignDoc)).ContinueOnAnyContext();
                await _bucketManager.RemoveDesignDocumentAsync("test").ContinueOnAnyContext();

                // If request is incorrectly awaiting on the current SynchronizationContext
                // We will see calls to Post or Send on the mock

                context.Verify(m => m.Post(It.IsAny<SendOrPostCallback>(), It.IsAny<object>()), Times.Never);
                context.Verify(m => m.Send(It.IsAny<SendOrPostCallback>(), It.IsAny<object>()), Times.Never);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        [Test]
        public void Flush_NoDeadlock()
        {
            // Using an asynchronous operation within an MVC Web API action causes
            // a deadlock if you wait for the result synchronously.

            var context = new Mock<SynchronizationContext>
            {
                CallBase = true
            };

            SynchronizationContext.SetSynchronizationContext(context.Object);
            try
            {
                _bucketManager.Flush();

                // If request is incorrectly awaiting on the current SynchronizationContext
                // We will see calls to Post or Send on the mock

                context.Verify(m => m.Post(It.IsAny<SendOrPostCallback>(), It.IsAny<object>()), Times.Never);
                context.Verify(m => m.Send(It.IsAny<SendOrPostCallback>(), It.IsAny<object>()), Times.Never);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        [Test]
        public async Task FlushAsync_NoDeadlock()
        {
            // Using an asynchronous operation within an MVC Web API action causes
            // a deadlock if you wait for the result synchronously.

            var context = new Mock<SynchronizationContext>
            {
                CallBase = true
            };

            SynchronizationContext.SetSynchronizationContext(context.Object);
            try
            {
                await _bucketManager.FlushAsync().ContinueOnAnyContext();

                // If request is incorrectly awaiting on the current SynchronizationContext
                // We will see calls to Post or Send on the mock

                context.Verify(m => m.Post(It.IsAny<SendOrPostCallback>(), It.IsAny<object>()), Times.Never);
                context.Verify(m => m.Send(It.IsAny<SendOrPostCallback>(), It.IsAny<object>()), Times.Never);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
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

