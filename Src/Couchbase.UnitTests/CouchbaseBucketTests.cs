using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Client.Providers;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.Utils;
using Couchbase.Views;
using Newtonsoft.Json;
using NUnit.Framework;
using Moq;

namespace Couchbase.UnitTests
{
    [TestFixture]
    public class CouchbaseBucketTests
    {

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

        #region Upsert Disposed Bucket

        [Test()]
        [ExpectedException(typeof(ObjectDisposedException))]
        public void UpsertDocument_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            bucket.Upsert(new Document<FakeDocument>
            {
                Id = "key",
                Content = new FakeDocument()
            });
        }

        [Test()]
        [ExpectedException(typeof(ObjectDisposedException))]
        public void UpsertDocumentReplicateTo_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            bucket.Upsert(new Document<FakeDocument>
            {
                Id = "key",
                Content = new FakeDocument()
            },
            ReplicateTo.One);
        }

        [Test()]
        [ExpectedException(typeof(ObjectDisposedException))]
        public void UpsertDocumentReplicateToPersistTo_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            bucket.Upsert(new Document<FakeDocument>
            {
                Id = "key",
                Content = new FakeDocument()
            },
            ReplicateTo.One, PersistTo.One);
        }

        [Test()]
        [ExpectedException(typeof(ObjectDisposedException))]
        public void UpsertKeyValue_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            bucket.Upsert("key", new FakeDocument());
        }

        [Test()]
        [ExpectedException(typeof(ObjectDisposedException))]
        public void UpsertKeyValueReplicateTo_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            bucket.Upsert("key", new FakeDocument(), ReplicateTo.One);
        }

        [Test()]
        [ExpectedException(typeof(ObjectDisposedException))]
        public void UpsertKeyValueReplicateToPersistTo_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            bucket.Upsert("key", new FakeDocument(), ReplicateTo.One, PersistTo.One);
        }

        [Test()]
        [ExpectedException(typeof(ObjectDisposedException))]
        public void UpsertKeyValueExpirationTS_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            bucket.Upsert("key", new FakeDocument(), TimeSpan.Zero);
        }

        [Test()]
        [ExpectedException(typeof(ObjectDisposedException))]
        public void UpsertKeyValueExpirationTSReplicateToPersistTo_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            bucket.Upsert("key", new FakeDocument(), TimeSpan.Zero, ReplicateTo.One, PersistTo.One);
        }

        [Test()]
        [ExpectedException(typeof(ObjectDisposedException))]
        public void UpsertKeyValueExpiration_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            bucket.Upsert("key", new FakeDocument(), 0U);
        }

        [Test()]
        [ExpectedException(typeof(ObjectDisposedException))]
        public void UpsertKeyValueExpirationReplicateToPersistTo_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            bucket.Upsert("key", new FakeDocument(), 0U, ReplicateTo.One, PersistTo.One);
        }

        [Test()]
        [ExpectedException(typeof(ObjectDisposedException))]
        public void UpsertKeyValueCas_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            bucket.Upsert("key", new FakeDocument(), 0UL);
        }

        [Test()]
        [ExpectedException(typeof(ObjectDisposedException))]
        public void UpsertKeyValueCasExpirationTS_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            bucket.Upsert("key", new FakeDocument(), 0UL, TimeSpan.Zero);
        }

        [Test()]
        [ExpectedException(typeof(ObjectDisposedException))]
        public void UpsertKeyValueCasExpirationTSReplicateToPersistTo_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            bucket.Upsert("key", new FakeDocument(), 0UL, TimeSpan.Zero, ReplicateTo.One, PersistTo.One);
        }

        [Test()]
        [ExpectedException(typeof(ObjectDisposedException))]
        public void UpsertKeyValueCasExpiration_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            bucket.Upsert("key", new FakeDocument(), 0UL, 0U);
        }

        [Test()]
        [ExpectedException(typeof(ObjectDisposedException))]
        public void UpsertKeyValueCasExpirationReplicateToPersistTo_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            bucket.Upsert("key", new FakeDocument(), 0UL, 0U, ReplicateTo.One, PersistTo.One);
        }

        [Test()]
        [ExpectedException(typeof(ObjectDisposedException))]
        public void UpsertDictionary_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            bucket.Upsert(new Dictionary<string, FakeDocument>
            {
                { "key", new FakeDocument() }
            });
        }

        [Test()]
        [ExpectedException(typeof(ObjectDisposedException))]
        public void UpsertDictionaryParallelOptions_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            bucket.Upsert(new Dictionary<string, FakeDocument>
            {
                { "key", new FakeDocument() }
            },
            new ParallelOptions());
        }

        [Test()]
        [ExpectedException(typeof(ObjectDisposedException))]
        public void UpsertDictionaryParallelOptionsRangeSize_DisposedBucket_ThrowsObjectDisposedException()
        {
            // Arrange

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            var bucket = new CouchbaseBucket(mockRequestExecuter.Object, new DefaultConverter(), new DefaultTranscoder());
            bucket.Dispose();

            // Act

            bucket.Upsert(new Dictionary<string, FakeDocument>
            {
                { "key", new FakeDocument() }
            },
            new ParallelOptions(), 0);
        }

        #endregion

        #endregion

        #region Helpers

        private class FakeDocument
        {
        }

        #endregion

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
