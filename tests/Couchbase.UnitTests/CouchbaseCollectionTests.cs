using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Retry;
using Couchbase.KeyValue;
using Couchbase.Management.Collections;
using Couchbase.Management.Views;
using Couchbase.Views;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests
{
    public class CouchbaseCollectionTests
    {
        [Fact]
        public void Get_Timed_Out_Throw_TimeoutException()
        {
            var collection = CreateTestCollection();

            Assert.ThrowsAsync<TimeoutException>(async () => await collection.GetAsync("key", options =>
            {
                options.Timeout(TimeSpan.FromMilliseconds(1d));
            }));
        }

        [Fact]
        public async Task SubDoc_More_Than_One_XAttr_Throws_ArgumentException()
        {
            var collection = CreateTestCollection();

            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await collection.LookupInAsync("docId", builder =>
                {
                    builder.Get("doc.path", isXattr: true);
                    builder.Count("path", isXattr: true);
                }, new LookupInOptions().Timeout(TimeSpan.FromHours(1)));
            });
        }

        //[Theory] //-> TODO fixup or find equivalant after retry commit
        //specific key value errors
        [InlineData(ResponseStatus.KeyNotFound, typeof(DocumentNotFoundException))]
        [InlineData(ResponseStatus.KeyExists, typeof(DocumentExistsException))]
        [InlineData(ResponseStatus.ValueTooLarge, typeof(ValueToolargeException))]
        [InlineData(ResponseStatus.InvalidArguments, typeof(InvalidArgumentException))]
        [InlineData(ResponseStatus.TemporaryFailure, typeof(TemporaryFailureException))]
        [InlineData(ResponseStatus.OperationTimeout, typeof(TimeoutException))]
        [InlineData(ResponseStatus.Locked, typeof(DocumentLockedException))]
        //durability errors
        [InlineData(ResponseStatus.DurabilityInvalidLevel, typeof(DurabilityLevelNotAvailableException))]
        [InlineData(ResponseStatus.DurabilityImpossible, typeof(DurabilityImpossibleException))]
        [InlineData(ResponseStatus.SyncWriteInProgress, typeof(DurableWriteInProgressException))]
        [InlineData(ResponseStatus.SyncWriteAmbiguous, typeof(DurabilityAmbiguousException))]
        //auth errors
        [InlineData(ResponseStatus.AuthenticationError, typeof(AuthenticationFailureException))]
        //internal errors
        //[InlineData(ResponseStatus.InternalError, typeof(InternalErrorException))]
        [InlineData(ResponseStatus.Eaccess, typeof(AuthenticationFailureException))]
        //[InlineData(ResponseStatus.Rollback, typeof(InternalErrorException))]
        //[InlineData(ResponseStatus.VBucketBelongsToAnotherServer, typeof(InternalErrorException))]
        [InlineData(ResponseStatus.AuthenticationContinue, typeof(AuthenticationFailureException))]
        [InlineData(ResponseStatus.AuthStale, typeof(AuthenticationFailureException))]
        //generic key-value errors
        [InlineData(ResponseStatus.InvalidRange, typeof(DeltaInvalidException))]
        //[InlineData(ResponseStatus.ItemNotStored, typeof(KeyValueException))]
       // [InlineData(ResponseStatus.IncrDecrOnNonNumericValue, typeof(KeyValueException))]
        //sub doc errors
        [InlineData(ResponseStatus.SubDocPathNotFound, typeof(PathNotFoundException))]
        [InlineData(ResponseStatus.SubDocPathMismatch, typeof(PathMismatchException))]
        [InlineData(ResponseStatus.SubDocPathInvalid, typeof(PathInvalidException))]
        [InlineData(ResponseStatus.SubDocPathTooBig, typeof(PathTooDeepException))]
        [InlineData(ResponseStatus.SubDocDocTooDeep, typeof(DocumentTooDeepException))]
        [InlineData(ResponseStatus.SubDocCannotInsert, typeof(ValueNotJsonException))]
        [InlineData(ResponseStatus.SubDocDocNotJson, typeof(DocumentNotJsonException))]
        [InlineData(ResponseStatus.SubDocNumRange, typeof(NumberTooBigException))]
        [InlineData( ResponseStatus.SubDocDeltaRange, typeof(DeltaInvalidException))]
        [InlineData(ResponseStatus.SubDocPathExists, typeof(PathExistsException))]
        [InlineData( ResponseStatus.SubDocValueTooDeep, typeof(ValueTooDeepException))]
        [InlineData(ResponseStatus.SubDocInvalidCombo, typeof(InvalidArgumentException))]
        //[InlineData(ResponseStatus.SubDocMultiPathFailure, typeof(InternalErrorException))]
        [InlineData(ResponseStatus.SubDocXattrInvalidFlagCombo, typeof(XattrException))]
        [InlineData(ResponseStatus.SubDocXattrInvalidKeyCombo, typeof(XattrException))]
        [InlineData( ResponseStatus.SubdocXattrUnknownMacro, typeof(XattrException))]
        [InlineData( ResponseStatus.SubdocXattrUnknownVattr, typeof(XattrException))]
        [InlineData( ResponseStatus.SubdocXattrCantModifyVattr, typeof(XattrException))]
        //[InlineData(ResponseStatus.SubdocMultiPathFailureDeleted, typeof(InternalErrorException))]
        [InlineData( ResponseStatus.SubdocInvalidXattrOrder, typeof(XattrException))]
        public async Task Get_Fails_Throw_KeyValueException(ResponseStatus responseStatus, Type exceptionType)
        {
            var collection = CreateTestCollection();

            try
            {
                using (await collection.GetAsync("key"))
                {
                }
            }
            catch (Exception e)
            {
                Assert.IsType(exceptionType, e);
            }
        }

        [Fact]
        public void Set_Factory_Test()
        {
            var collection = CreateTestCollection();

            var set = collection.Set<dynamic>("theDocId");
            Assert.NotNull(set);
        }

        [Fact]
        public void Queue_Factory_Test()
        {
            var collection = CreateTestCollection();

            var queue = collection.Queue<dynamic>("theDocId");
            Assert.NotNull(queue);
        }

        [Fact]
        public void List_Factory_Test()
        {
            var collection = CreateTestCollection();

            var list = collection.List<dynamic>("theDocId");
            Assert.NotNull(list);
        }

        [Fact]
        public void Dictionary_Factory_Test()
        {
            var collection = CreateTestCollection();

            var dict = collection.Dictionary<string, dynamic>("theDocId");
            Assert.NotNull(dict);
        }

        [Fact]
        public void GetAsync_Allows_No_GetOptions()
        {
            var collection = CreateTestCollection();

            collection.GetAsync("key").GetAwaiter().GetResult();
        }

        internal class FakeBucket : BucketBase
        {
            private Queue<ResponseStatus> _statuses = new Queue<ResponseStatus>();
            public FakeBucket(params ResponseStatus[] statuses)
                : base("fake", new ClusterContext(), new Mock<IScopeFactory>().Object, new Mock<IRetryOrchestrator>().Object, new Mock<ILogger>().Object)
            {
                foreach (var responseStatuse in statuses)
                {
                    _statuses.Enqueue(responseStatuse);
                }
            }

            public override IViewIndexManager ViewIndexes => throw new NotImplementedException();

            public override ICollectionManager Collections => throw new NotImplementedException();

            internal override async Task SendAsync(IOperation op, CancellationToken token = default, TimeSpan? timeout = null)
            {
                var mockConnection = new Mock<IConnection>();
                mockConnection.SetupGet(x => x.IsDead).Returns(false);
                mockConnection
                    .Setup(x => x.SendAsync(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<Func<SocketAsyncState, Task>>()))
                    .Returns(Task.CompletedTask);

                var clusterNode = new ClusterNode(new ClusterContext(), new Mock<IConnectionFactory>().Object, new Mock<ILogger<ClusterNode>>().Object)
                {
                    Connection = mockConnection.Object
                };
                await clusterNode.ExecuteOp(op, token, timeout);

                if (_statuses.TryDequeue(out ResponseStatus status))
                {
                    await op.Completed(new SocketAsyncState
                    {
                        Status = status
                    });
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            public override IScope this[string name] => throw new NotImplementedException();

            public override Task<IViewResult<TKey, TValue>> ViewQueryAsync<TKey, TValue>(string designDocument, string viewName, ViewOptions options = null)
            {
                throw new NotImplementedException();
            }

            internal override Task BootstrapAsync(IClusterNode bootstrapNodes)
            {
                throw new NotImplementedException();
            }

            internal override void ConfigUpdated(object sender, BucketConfigEventArgs e)
            {
                throw new NotImplementedException();
            }
        }

        private static CouchbaseCollection CreateTestCollection()
        {
            var mockBucket = new Mock<FakeBucket>();
            return new CouchbaseCollection(mockBucket.Object, new DefaultTranscoder(),
                new Mock<ILogger<CouchbaseCollection>>().Object,
                null, CouchbaseCollection.DefaultCollectionName, Scope.DefaultScopeName);
        }
    }
}
