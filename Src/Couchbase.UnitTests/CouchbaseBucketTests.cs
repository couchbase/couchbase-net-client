using System;
using System.Collections.Generic;
using System.Security.Authentication;
using System.Threading.Tasks;
using Couchbase.Authentication;
using Couchbase.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Providers;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.Core.Serialization;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.Utils;
using NUnit.Framework;
using Moq;

namespace Couchbase.UnitTests
{
    [TestFixture]
    public class CouchbaseBucketTests
    {
        [Test]
        public void GetAndLockAsync_Does_Not_Throw_StackOverFlowException()
        {
            var operationResult = new Mock<IOperationResult<dynamic>>();
            operationResult.SetupGet(m => m.Success).Returns(true);
            operationResult.SetupGet(m => m.Status).Returns(ResponseStatus.Success);

            var mockRequestExecutor = new Mock<IRequestExecuter>();
            mockRequestExecutor.Setup(x => x.SendWithRetryAsync(It.IsAny<IOperation<dynamic>>(), null, null))
                .Returns(Task.FromResult(operationResult.Object));

            var bucket = new CouchbaseBucket(mockRequestExecutor.Object, new DefaultConverter(),
                new DefaultTranscoder());
            var result = bucket.GetAndLockAsync<dynamic>("thekey", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        }

        public void InsertAsync_Does_Not_Throw_StackOverFlowException()
        {
            var operationResult = new Mock<IOperationResult<dynamic>>();
            operationResult.SetupGet(m => m.Success).Returns(true);
            operationResult.SetupGet(m => m.Status).Returns(ResponseStatus.Success);

            var mockRequestExecutor = new Mock<IRequestExecuter>();
            mockRequestExecutor.Setup(x => x.SendWithRetryAsync(It.IsAny<IOperation<dynamic>>(), null, null))
                .Returns(Task.FromResult(operationResult.Object));

            var bucket = new CouchbaseBucket(mockRequestExecutor.Object, new DefaultConverter(), new DefaultTranscoder());

            var result =  bucket.InsertAsync<dynamic>("itskey", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        }

        #region Exists/ExistsAsync

        [TestCase(KeyState.FoundPersisted, true)]
        [TestCase(KeyState.FoundNotPersisted, true)]
        [TestCase(KeyState.NotFound, false)]
        [TestCase(KeyState.LogicalDeleted, false)]
        public void Exists_AnyKeyState_ReturnsExpectedResult(KeyState keyState, bool expectedResult)
        {
            // Arrange

            var operationResult = new Mock<IOperationResult<ObserveState>>();
            operationResult.SetupGet(m => m.Success).Returns(true);
            operationResult.SetupGet(m => m.Status).Returns(ResponseStatus.Success);
            operationResult.SetupGet(m => m.Value).Returns(new ObserveState()
            {
                KeyState = keyState
            });

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            mockRequestExecuter.Setup(m => m.SendWithRetry(It.IsAny<Observe>())).Returns(operationResult.Object);

            // Act

            bool result;
            using (var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder()))
            {
                result = bucket.Exists("key");
            }

            // Assert

            Assert.AreEqual(expectedResult, result);
        }

        [TestCase(KeyState.FoundPersisted, true)]
        [TestCase(KeyState.FoundNotPersisted, true)]
        [TestCase(KeyState.NotFound, false)]
        [TestCase(KeyState.LogicalDeleted, false)]
        public void ExistsAsync_AnyKeyState_ReturnsExpectedResult(KeyState keyState, bool expectedResult)
        {
            // Arrange

            var operationResult = new Mock<IOperationResult<ObserveState>>();
            operationResult.SetupGet(m => m.Success).Returns(true);
            operationResult.SetupGet(m => m.Status).Returns(ResponseStatus.Success);
            operationResult.SetupGet(m => m.Value).Returns(new ObserveState()
            {
                KeyState = keyState
            });

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            mockRequestExecuter.Setup(m => m.SendWithRetryAsync(It.IsAny<Observe>(), null, null)).Returns(Task.FromResult(operationResult.Object));

            // Act

            bool result;
            using (var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder()))
            {
                result = bucket.ExistsAsync("key").Result;
            }

            // Assert

            Assert.AreEqual(expectedResult, result);
        }

        #endregion

        #region Upsert

        [Test]
        public async Task UpsertAsyncOverloadPassesExpirationAndCasParametersAsExpected()
        {
            var mockOperationResult = new Mock<IOperationResult<dynamic>>();

            var mockRequestExecutor = new Mock<IRequestExecuter>();
            mockRequestExecutor.Setup(x => x.SendWithDurabilityAsync(It.IsAny<Set<dynamic>>(), false, ReplicateTo.One, PersistTo.One, null, null))
                .Returns(Task.FromResult(mockOperationResult.Object));

            var bucket = new CouchbaseBucket(mockRequestExecutor.Object, new DefaultConverter(), new DefaultTranscoder());

            await bucket.UpsertAsync<dynamic>("key", new { }, TimeSpan.FromSeconds(10), ReplicateTo.One, PersistTo.One, TimeSpan.FromSeconds(5));

            mockRequestExecutor.Verify(x => x.SendWithDurabilityAsync(
                It.Is<Set<dynamic>>(set => set.Expires == 10 && set.Cas == 0 && set.Timeout == 5),
                false,
                ReplicateTo.One,
                PersistTo.One,
                null,
                null)
            );
        }

        #region Upsert Disposed Bucket

        [Test()]
        public void UpsertDocument_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            Assert.Throws<ObjectDisposedException>(() => bucket.Upsert(new Document<FakeDocument>
            {
                Id = "key",
                Content = new FakeDocument()
            }));
        }

        [Test()]
        public void UpsertDocumentReplicateTo_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            Assert.Throws<ObjectDisposedException>(() => bucket.Upsert(new Document<FakeDocument>
            {
                Id = "key",
                Content = new FakeDocument()
            },
            ReplicateTo.One));
        }

        [Test()]
        public void UpsertDocumentReplicateToPersistTo_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            Assert.Throws<ObjectDisposedException>(() => bucket.Upsert(new Document<FakeDocument>
            {
                Id = "key",
                Content = new FakeDocument()
            },
            ReplicateTo.One, PersistTo.One));
        }

        [Test()]
        public void UpsertKeyValue_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            Assert.Throws<ObjectDisposedException>(() => bucket.Upsert("key", new FakeDocument()));
        }

        [Test()]
        public void UpsertKeyValueReplicateTo_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            Assert.Throws<ObjectDisposedException>(() => bucket.Upsert("key", new FakeDocument(), ReplicateTo.One));
        }

        [Test()]
        public void UpsertKeyValueReplicateToPersistTo_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            Assert.Throws<ObjectDisposedException>(() => bucket.Upsert("key", new FakeDocument(), ReplicateTo.One, PersistTo.One));
        }

        [Test()]
        public void UpsertKeyValueExpirationTS_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            Assert.Throws<ObjectDisposedException>(() => bucket.Upsert("key", new FakeDocument(), TimeSpan.Zero));
        }

        [Test()]
        public void UpsertKeyValueExpirationTSReplicateToPersistTo_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            Assert.Throws<ObjectDisposedException>(() => bucket.Upsert("key", new FakeDocument(), TimeSpan.Zero, ReplicateTo.One, PersistTo.One));
        }

        [Test()]
        public void UpsertKeyValueExpiration_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            Assert.Throws<ObjectDisposedException>(() => bucket.Upsert("key", new FakeDocument(), 0U));
        }

        [Test()]
        public void UpsertKeyValueExpirationReplicateToPersistTo_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            Assert.Throws<ObjectDisposedException>(() => bucket.Upsert("key", new FakeDocument(), 0U, ReplicateTo.One, PersistTo.One));
        }

        [Test()]
        public void UpsertKeyValueCas_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            Assert.Throws<ObjectDisposedException>(() => bucket.Upsert("key", new FakeDocument(), 0UL));
        }

        [Test()]
        public void UpsertKeyValueCasExpirationTS_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            Assert.Throws<ObjectDisposedException>(() => bucket.Upsert("key", new FakeDocument(), 0UL, TimeSpan.Zero));
        }

        [Test()]
        public void UpsertKeyValueCasExpirationTSReplicateToPersistTo_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            Assert.Throws<ObjectDisposedException>(() => bucket.Upsert("key", new FakeDocument(), 0UL, TimeSpan.Zero, ReplicateTo.One, PersistTo.One));
        }

        [Test()]
        public void UpsertKeyValueCasExpiration_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            Assert.Throws<ObjectDisposedException>(() => bucket.Upsert("key", new FakeDocument(), 0UL, 0U));
        }

        [Test()]
        public void UpsertKeyValueCasExpirationReplicateToPersistTo_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            Assert.Throws<ObjectDisposedException>(() => bucket.Upsert("key", new FakeDocument(), 0UL, 0U, ReplicateTo.One, PersistTo.One));
        }

        [Test()]
        public void UpsertDictionary_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            Assert.Throws<ObjectDisposedException>(() => bucket.Upsert(new Dictionary<string, FakeDocument>
            {
                { "key", new FakeDocument() }
            }));
        }

        [Test()]
        public void UpsertDictionaryParallelOptions_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            Assert.Throws<ObjectDisposedException>(() => bucket.Upsert(new Dictionary<string, FakeDocument>
            {
                { "key", new FakeDocument() }
            },
            new ParallelOptions()));
        }

        [Test()]
        public void UpsertDictionaryParallelOptionsRangeSize_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            Assert.Throws<ObjectDisposedException>(() => bucket.Upsert(new Dictionary<string, FakeDocument>
            {
                { "key", new FakeDocument() }
            },
            new ParallelOptions(), 0));
        }

        #endregion

        #endregion

        #region Helpers

        private class FakeDocument
        {
        }

        #endregion

        #region DataStructures

        [Test]
        public void QueueSize_Returns_Size_If_Document_Exists()
        {
            var items = new List<object> { 1, 2, 3 };
            var mockExecutor = new Mock<IRequestExecuter>();
            mockExecutor.Setup(x => x.SendWithRetry(It.IsAny<Get<List<object>>>()))
                .Returns(new OperationResult<List<object>> { Success = true, Value = items })
                .Verifiable();

            var bucket = new CouchbaseBucket(mockExecutor.Object, new DefaultConverter(), new DefaultTranscoder());

            var result = bucket.QueueSize("my_queue");

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(items.Count, result.Value);
            mockExecutor.Verify();
        }

        [Test]
        public void QueueSize_Returns_Error_Result_If_Document_Does_Not_Exists()
        {
            var mockExecutor = new Mock<IRequestExecuter>();
            mockExecutor.Setup(x => x.SendWithRetry(It.IsAny<Get<List<object>>>()))
                .Returns(new OperationResult<List<object>> { Success = false })
                .Verifiable();

            var bucket = new CouchbaseBucket(mockExecutor.Object, new DefaultConverter(), new DefaultTranscoder());

            var result = bucket.QueueSize("my_queue");

            Assert.IsNotNull(result);
            Assert.IsFalse(result.Success);
            mockExecutor.Verify();
        }

        [Test]
        public async Task QueueSizeAsync_Returns_Size_If_Document_Exists()
        {
            var items = new List<object> { 1, 2, 3 };
            var mockExecutor = new Mock<IRequestExecuter>();
            mockExecutor.Setup(x => x.SendWithRetryAsync(It.IsAny<Get<List<object>>>(), null, null))
                .ReturnsAsync(new OperationResult<List<object>> { Success = true, Value = items })
                .Verifiable();

            var bucket = new CouchbaseBucket(mockExecutor.Object, new DefaultConverter(), new DefaultTranscoder());

            var result = await bucket.QueueSizeAsync("my_queue");

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(items.Count, result.Value);
            mockExecutor.Verify();
        }

        [Test]
        public async Task QueueSizeAsync_Returns_Error_Result_If_Document_Does_Not_Exists()
        {
            var mockExecutor = new Mock<IRequestExecuter>();
            mockExecutor.Setup(x => x.SendWithRetryAsync(It.IsAny<Get<List<object>>>(), null, null))
                .ReturnsAsync(new OperationResult<List<object>> { Success = false })
                .Verifiable();

            var bucket = new CouchbaseBucket(mockExecutor.Object, new DefaultConverter(), new DefaultTranscoder());

            var result = await bucket.QueueSizeAsync("my_queue");

            Assert.IsNotNull(result);
            Assert.IsFalse(result.Success);

            mockExecutor.Verify();
        }

        #endregion

        #region LookupIn / MutateIn

        [Test]
        public void Executing_LookupIn_With_XATTR_Throw_ServiceNotSupportedException_When_SubdocXAttributes_Is_False_And()
        {
            var mockController = new Mock<IClusterController>();
            mockController.Setup(x => x.Configuration).Returns(new ClientConfiguration());
            var mockCredentials = new Mock<IAuthenticator>();

            var mockConfig = new Mock<IConfigInfo>();
            mockConfig.Setup(x => x.BucketConfig).Returns(new BucketConfig());
            mockConfig.Setup(x => x.ClientConfig).Returns(new ClientConfiguration());
            mockConfig.Setup(x => x.SupportsSubdocXAttributes).Returns(false);

            var couchbaseBucket = new CouchbaseBucket(mockController.Object, "default", new DefaultConverter(), new DefaultTranscoder(), mockCredentials.Object);
            (couchbaseBucket as IConfigObserver).NotifyConfigChanged(mockConfig.Object);

            var result = couchbaseBucket.LookupIn<dynamic>("key")
                .Get("username", SubdocPathFlags.Xattr)
                .Execute();

            Assert.IsFalse(result.Success);
            Assert.IsInstanceOf<FeatureNotAvailableException>(result.Exception, ExceptionUtil.XAttriburesNotAvailableMessage);
        }

        [Test]
        public async Task Executing_LookupInAsync_With_XATTR_Throw_ServiceNotSupportedException_When_SubdocXAttributes_Is_False_And()
        {
            var mockController = new Mock<IClusterController>();
            mockController.Setup(x => x.Configuration).Returns(new ClientConfiguration());
            var mockCredentials = new Mock<IAuthenticator>();

            var mockConfig = new Mock<IConfigInfo>();
            mockConfig.Setup(x => x.BucketConfig).Returns(new BucketConfig());
            mockConfig.Setup(x => x.ClientConfig).Returns(new ClientConfiguration());
            mockConfig.Setup(x => x.SupportsSubdocXAttributes).Returns(false);

            var couchbaseBucket = new CouchbaseBucket(mockController.Object, "default", new DefaultConverter(), new DefaultTranscoder(), mockCredentials.Object);
            (couchbaseBucket as IConfigObserver).NotifyConfigChanged(mockConfig.Object);

            var result = await couchbaseBucket.LookupIn<dynamic>("key")
                .Get("username", SubdocPathFlags.Xattr)
                .ExecuteAsync();

            Assert.IsFalse(result.Success);
            Assert.IsInstanceOf<FeatureNotAvailableException>(result.Exception, ExceptionUtil.XAttriburesNotAvailableMessage);
        }

        [Test]
        public void Executing_MutateIn_With_XATTR_Throw_ServiceNotSupportedException_When_SubdocXAttributes_Is_False_And()
        {
            var mockController = new Mock<IClusterController>();
            mockController.Setup(x => x.Configuration).Returns(new ClientConfiguration());
            var mockCredentials = new Mock<IAuthenticator>();

            var mockConfig = new Mock<IConfigInfo>();
            mockConfig.Setup(x => x.BucketConfig).Returns(new BucketConfig());
            mockConfig.Setup(x => x.ClientConfig).Returns(new ClientConfiguration());
            mockConfig.Setup(x => x.SupportsSubdocXAttributes).Returns(false);

            var couchbaseBucket = new CouchbaseBucket(mockController.Object, "default", new DefaultConverter(), new DefaultTranscoder(), mockCredentials.Object);
            (couchbaseBucket as IConfigObserver).NotifyConfigChanged(mockConfig.Object);

            var result = couchbaseBucket.MutateIn<dynamic>("key")
                .Upsert("username", "value", SubdocPathFlags.Xattr)
                .Execute();

            Assert.IsFalse(result.Success);
            Assert.IsInstanceOf<FeatureNotAvailableException>(result.Exception, ExceptionUtil.XAttriburesNotAvailableMessage);
        }

        [Test]
        public async Task Executing_MutateInAsync_With_XATTR_Throw_ServiceNotSupportedException_When_SubdocXAttributes_Is_False_And()
        {
            var mockController = new Mock<IClusterController>();
            mockController.Setup(x => x.Configuration).Returns(new ClientConfiguration());
            var mockCredentials = new Mock<IAuthenticator>();

            var mockConfig = new Mock<IConfigInfo>();
            mockConfig.Setup(x => x.BucketConfig).Returns(new BucketConfig());
            mockConfig.Setup(x => x.ClientConfig).Returns(new ClientConfiguration());
            mockConfig.Setup(x => x.SupportsSubdocXAttributes).Returns(false);

            var couchbaseBucket = new CouchbaseBucket(mockController.Object, "default", new DefaultConverter(), new DefaultTranscoder(), mockCredentials.Object);
            (couchbaseBucket as IConfigObserver).NotifyConfigChanged(mockConfig.Object);

            var result = await couchbaseBucket.MutateIn<dynamic>("key")
                .Upsert("username", "value", SubdocPathFlags.Xattr)
                .ExecuteAsync();

            Assert.IsFalse(result.Success);
            Assert.IsInstanceOf<FeatureNotAvailableException>(result.Exception, ExceptionUtil.XAttriburesNotAvailableMessage);
        }

        #endregion

        [Test]
        public void CreateManager_With_Null_Authenticator_Throws_AuthenticationException()
        {
            var mockController = new Mock<IClusterController>();
            mockController.Setup(x => x.Configuration).Returns(new ClientConfiguration());

            var bucket = new CouchbaseBucket(mockController.Object, "", new DefaultConverter(), new DefaultTranscoder(), null);

            Assert.Throws<AuthenticationException>(() => bucket.CreateManager());
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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
