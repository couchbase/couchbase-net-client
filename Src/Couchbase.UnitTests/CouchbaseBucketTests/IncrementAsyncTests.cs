﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Buckets;
using Couchbase.IO.Operations;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests
{
    public partial class CouchbaseBucketTests
    {
        [TestFixture]
        public class IncrementAsyncTests : CouchbaseBucketTests
        {
            [Test]
            public async Task IncrementAsync_With_Key_ExecutesCorrectOperation()
            {
                // Arrange

                Increment operation = null;

                var mockRequestExecuter = new Mock<IRequestExecuter>();
                mockRequestExecuter
                    .Setup(m => m.SendWithRetryAsync(It.IsAny<Increment>(), null, null))
                    .Callback((IOperation<ulong> op, TaskCompletionSource<IOperationResult<ulong>> tcs, CancellationTokenSource ccs) => operation = (Increment)op)
                    .ReturnsAsync((IOperationResult<ulong>)null);

                // Act

                using (var bucket = new CouchbaseBucket(mockRequestExecuter.Object, _converter, _transcoder))
                {
                    bucket.Name = "bucket";

                    await bucket.IncrementAsync("key");
                }

                // Assert

                Assert.NotNull(operation);
                Assert.AreEqual(1, operation.Delta);
                Assert.AreEqual(1, operation.Initial);
                Assert.AreEqual("bucket", operation.BucketName);
                Assert.AreEqual(0, operation.Expires);
                Assert.AreEqual(42, operation.Timeout);
            }

            [Test]
            public async Task IncrementAsync_With_Timeout_ExecutesCorrectOperation()
            {
                // Arrange

                Increment operation = null;

                var mockRequestExecuter = new Mock<IRequestExecuter>();
                mockRequestExecuter
                    .Setup(m => m.SendWithRetryAsync(It.IsAny<Increment>(), null, null))
                    .Callback((IOperation<ulong> op, TaskCompletionSource<IOperationResult<ulong>> tcs, CancellationTokenSource ccs) => operation = (Increment)op)
                    .ReturnsAsync((IOperationResult<ulong>)null);

                // Act

                using (var bucket = new CouchbaseBucket(mockRequestExecuter.Object, _converter, _transcoder))
                {
                    bucket.Name = "bucket";

                    await bucket.IncrementAsync("key", TimeSpan.FromSeconds(10));
                }

                // Assert

                Assert.NotNull(operation);
                Assert.AreEqual(1, operation.Delta);
                Assert.AreEqual(1, operation.Initial);
                Assert.AreEqual("bucket", operation.BucketName);
                Assert.AreEqual(0, operation.Expires);
                Assert.AreEqual(10, operation.Timeout);
            }

            [Test]
            public async Task IncrementAsync_With_Delta_ExecutesCorrectOperation()
            {
                // Arrange

                Increment operation = null;

                var mockRequestExecuter = new Mock<IRequestExecuter>();
                mockRequestExecuter
                    .Setup(m => m.SendWithRetryAsync(It.IsAny<Increment>(), null, null))
                    .Callback((IOperation<ulong> op, TaskCompletionSource<IOperationResult<ulong>> tcs, CancellationTokenSource ccs) => operation = (Increment)op)
                    .ReturnsAsync((IOperationResult<ulong>)null);

                // Act

                using (var bucket = new CouchbaseBucket(mockRequestExecuter.Object, _converter, _transcoder))
                {
                    bucket.Name = "bucket";

                    await bucket.IncrementAsync("key", 2);
                }

                // Assert

                Assert.NotNull(operation);
                Assert.AreEqual(2, operation.Delta);
                Assert.AreEqual(1, operation.Initial);
                Assert.AreEqual("bucket", operation.BucketName);
                Assert.AreEqual(0, operation.Expires);
                Assert.AreEqual(42, operation.Timeout);
            }

            [Test]
            public async Task IncrementAsync_With_DeltaTimeout_ExecutesCorrectOperation()
            {
                // Arrange

                Increment operation = null;

                var mockRequestExecuter = new Mock<IRequestExecuter>();
                mockRequestExecuter
                    .Setup(m => m.SendWithRetryAsync(It.IsAny<Increment>(), null, null))
                    .Callback((IOperation<ulong> op, TaskCompletionSource<IOperationResult<ulong>> tcs, CancellationTokenSource ccs) => operation = (Increment)op)
                    .ReturnsAsync((IOperationResult<ulong>)null);

                // Act

                using (var bucket = new CouchbaseBucket(mockRequestExecuter.Object, _converter, _transcoder))
                {
                    bucket.Name = "bucket";

                    await bucket.IncrementAsync("key", 2, TimeSpan.FromSeconds(10));
                }

                // Assert

                Assert.NotNull(operation);
                Assert.AreEqual(2, operation.Delta);
                Assert.AreEqual(1, operation.Initial);
                Assert.AreEqual("bucket", operation.BucketName);
                Assert.AreEqual(0, operation.Expires);
                Assert.AreEqual(10, operation.Timeout);
            }

            [Test]
            public async Task IncrementAsync_With_DeltaInitial_ExecutesCorrectOperation()
            {
                // Arrange

                Increment operation = null;

                var mockRequestExecuter = new Mock<IRequestExecuter>();
                mockRequestExecuter
                    .Setup(m => m.SendWithRetryAsync(It.IsAny<Increment>(), null, null))
                    .Callback((IOperation<ulong> op, TaskCompletionSource<IOperationResult<ulong>> tcs, CancellationTokenSource ccs) => operation = (Increment)op)
                    .ReturnsAsync((IOperationResult<ulong>)null);

                // Act

                using (var bucket = new CouchbaseBucket(mockRequestExecuter.Object, _converter, _transcoder))
                {
                    bucket.Name = "bucket";

                    await bucket.IncrementAsync("key", 2, 4);
                }

                // Assert

                Assert.NotNull(operation);
                Assert.AreEqual(2, operation.Delta);
                Assert.AreEqual(4, operation.Initial);
                Assert.AreEqual("bucket", operation.BucketName);
                Assert.AreEqual(0, operation.Expires);
                Assert.AreEqual(42, operation.Timeout);
            }

            [Test]
            public async Task IncrementAsync_With_DeltaInitialExpirationTime_ExecutesCorrectOperation()
            {
                // Arrange

                Increment operation = null;

                var mockRequestExecuter = new Mock<IRequestExecuter>();
                mockRequestExecuter
                    .Setup(m => m.SendWithRetryAsync(It.IsAny<Increment>(), null, null))
                    .Callback((IOperation<ulong> op, TaskCompletionSource<IOperationResult<ulong>> tcs, CancellationTokenSource ccs) => operation = (Increment)op)
                    .ReturnsAsync((IOperationResult<ulong>)null);

                // Act

                using (var bucket = new CouchbaseBucket(mockRequestExecuter.Object, _converter, _transcoder))
                {
                    bucket.Name = "bucket";

                    await bucket.IncrementAsync("key", 2, 4, TimeSpan.FromSeconds(10));
                }

                // Assert

                Assert.NotNull(operation);
                Assert.AreEqual(2, operation.Delta);
                Assert.AreEqual(4, operation.Initial);
                Assert.AreEqual("bucket", operation.BucketName);
                Assert.AreEqual(10, operation.Expires);
                Assert.AreEqual(42, operation.Timeout);
            }

            [Test]
            public async Task IncrementAsync_With_DeltaInitialExpiration_ExecutesCorrectOperation()
            {
                // Arrange

                Increment operation = null;

                var mockRequestExecuter = new Mock<IRequestExecuter>();
                mockRequestExecuter
                    .Setup(m => m.SendWithRetryAsync(It.IsAny<Increment>(), null, null))
                    .Callback((IOperation<ulong> op, TaskCompletionSource<IOperationResult<ulong>> tcs, CancellationTokenSource ccs) => operation = (Increment)op)
                    .ReturnsAsync((IOperationResult<ulong>)null);

                // Act

                using (var bucket = new CouchbaseBucket(mockRequestExecuter.Object, _converter, _transcoder))
                {
                    bucket.Name = "bucket";

                    await bucket.IncrementAsync("key", 2, 4, 10);
                }

                // Assert

                Assert.NotNull(operation);
                Assert.AreEqual(2, operation.Delta);
                Assert.AreEqual(4, operation.Initial);
                Assert.AreEqual("bucket", operation.BucketName);
                Assert.AreEqual(10, operation.Expires);
                Assert.AreEqual(42, operation.Timeout);
            }

            [Test]
            public async Task IncrementAsync_With_DeltaInitialExpirationTimeTimeout_ExecutesCorrectOperation()
            {
                // Arrange

                Increment operation = null;

                var mockRequestExecuter = new Mock<IRequestExecuter>();
                mockRequestExecuter
                    .Setup(m => m.SendWithRetryAsync(It.IsAny<Increment>(), null, null))
                    .Callback((IOperation<ulong> op, TaskCompletionSource<IOperationResult<ulong>> tcs, CancellationTokenSource ccs) => operation = (Increment)op)
                    .ReturnsAsync((IOperationResult<ulong>)null);

                // Act

                using (var bucket = new CouchbaseBucket(mockRequestExecuter.Object, _converter, _transcoder))
                {
                    bucket.Name = "bucket";

                    await bucket.IncrementAsync("key", 2, 4, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20));
                }

                // Assert

                Assert.NotNull(operation);
                Assert.AreEqual(2, operation.Delta);
                Assert.AreEqual(4, operation.Initial);
                Assert.AreEqual("bucket", operation.BucketName);
                Assert.AreEqual(10, operation.Expires);
                Assert.AreEqual(20, operation.Timeout);
            }

            [Test]
            public async Task IncrementAsync_With_DeltaInitialExpirationTimeout_ExecutesCorrectOperation()
            {
                // Arrange

                Increment operation = null;

                var mockRequestExecuter = new Mock<IRequestExecuter>();
                mockRequestExecuter
                    .Setup(m => m.SendWithRetryAsync(It.IsAny<Increment>(), null, null))
                    .Callback((IOperation<ulong> op, TaskCompletionSource<IOperationResult<ulong>> tcs, CancellationTokenSource ccs) => operation = (Increment)op)
                    .ReturnsAsync((IOperationResult<ulong>)null);

                // Act

                using (var bucket = new CouchbaseBucket(mockRequestExecuter.Object, _converter, _transcoder))
                {
                    bucket.Name = "bucket";

                    await bucket.IncrementAsync("key", 2, 4, 10, TimeSpan.FromSeconds(20));
                }

                // Assert

                Assert.NotNull(operation);
                Assert.AreEqual(2, operation.Delta);
                Assert.AreEqual(4, operation.Initial);
                Assert.AreEqual("bucket", operation.BucketName);
                Assert.AreEqual(10, operation.Expires);
                Assert.AreEqual(20, operation.Timeout);
            }
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
