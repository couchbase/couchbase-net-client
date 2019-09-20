using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Legacy;
using Couchbase.Management;
using Couchbase.Management.Collections;
using Couchbase.Services.Views;
using Moq;
using Xunit;

namespace Couchbase.UnitTests
{
    public class CouchbaseCollectionTests
    {
        [Fact]
        public void Get_Timed_Out_Throw_TimeoutException()
        {
            var mockBucket = new Mock<FakeBucket>();
            var collection = new CouchbaseCollection(mockBucket.Object, 0, "_default");

            Assert.ThrowsAsync<TimeoutException>(async () => await collection.GetAsync("key", options =>
            {
                options.WithTimeout(TimeSpan.FromMilliseconds(1d));
            }));
        }

        [Fact]
        public async Task SubDoc_More_Than_One_XAttr_Throws_ArgumentException()
        {
            var mockBucket = new Mock<FakeBucket>();
            var collection = new CouchbaseCollection(mockBucket.Object, 0, "_default");

            Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await collection.LookupInAsync("docId", builder =>
                {
                    builder.Get("doc.path", isXattr: true);
                    builder.Count("path", isXattr: true);
                }, new LookupInOptions {Timeout = TimeSpan.FromHours(1)});
            });
        }

        [Theory]
        //specific key value errors
        [InlineData(ResponseStatus.KeyNotFound, typeof(KeyNotFoundException))]
        [InlineData(ResponseStatus.KeyExists, typeof(KeyExistsException))]
        [InlineData(ResponseStatus.ValueTooLarge, typeof(ValueTooLargeException))]
        [InlineData(ResponseStatus.InvalidArguments, typeof(InvalidArgumentException))]
        [InlineData(ResponseStatus.TemporaryFailure, typeof(TempFailException))]
        [InlineData(ResponseStatus.OperationTimeout, typeof(TimeoutException))]
        [InlineData(ResponseStatus.Locked, typeof(KeyLockedException))]
        //durability errors
        [InlineData(ResponseStatus.DurabilityInvalidLevel, typeof(DurabilityException))]
        [InlineData(ResponseStatus.DurabilityImpossible, typeof(DurabilityException))]
        [InlineData(ResponseStatus.SyncWriteInProgress, typeof(DurabilityException))]
        [InlineData(ResponseStatus.SyncWriteAmbiguous, typeof(DurabilityException))]
        //auth errors
        [InlineData(ResponseStatus.AuthenticationError, typeof(AuthenticationException))]
        //internal errors
        [InlineData(ResponseStatus.InternalError, typeof(InternalErrorException))]
        [InlineData(ResponseStatus.Eaccess, typeof(AuthenticationException))]
        [InlineData(ResponseStatus.Rollback, typeof(InternalErrorException))]
        [InlineData(ResponseStatus.VBucketBelongsToAnotherServer, typeof(InternalErrorException))]
        [InlineData(ResponseStatus.AuthenticationContinue, typeof(InternalErrorException))]
        [InlineData(ResponseStatus.AuthStale, typeof(InternalErrorException))]
        //generic key-value errors
        [InlineData(ResponseStatus.InvalidRange, typeof(KeyValueException))]
        [InlineData(ResponseStatus.ItemNotStored, typeof(KeyValueException))]
        [InlineData(ResponseStatus.IncrDecrOnNonNumericValue, typeof(KeyValueException))]
        //sub doc errors
        [InlineData(ResponseStatus.SubDocPathNotFound, typeof(PathNotFoundException))]
        [InlineData(ResponseStatus.SubDocPathMismatch, typeof(PathMismatchException))]
        [InlineData(ResponseStatus.SubDocPathInvalid, typeof(PathInvalidException))]
        [InlineData(ResponseStatus.SubDocPathTooBig, typeof(PathTooBigException))]
        [InlineData(ResponseStatus.SubDocDocTooDeep, typeof(KeyValueException))]
        [InlineData(ResponseStatus.SubDocCannotInsert, typeof(KeyValueException))]
        [InlineData(ResponseStatus.SubDocDocNotJson, typeof(KeyValueException))]
        [InlineData(ResponseStatus.SubDocNumRange, typeof(KeyValueException))]
        [InlineData( ResponseStatus.SubDocDeltaRange, typeof(KeyValueException))]
        [InlineData(ResponseStatus.SubDocPathExists, typeof(KeyValueException))]
        [InlineData( ResponseStatus.SubDocValueTooDeep, typeof(KeyValueException))]
        [InlineData(ResponseStatus.SubDocInvalidCombo, typeof(KeyValueException))]
        [InlineData(ResponseStatus.SubDocMultiPathFailure, typeof(InternalErrorException))]
        [InlineData(ResponseStatus.SubDocXattrInvalidFlagCombo, typeof(InternalErrorException))]
        [InlineData(ResponseStatus.SubDocXattrInvalidKeyCombo, typeof(InternalErrorException))]
        [InlineData( ResponseStatus.SubdocXattrUnknownMacro, typeof(KeyValueException))]
        [InlineData( ResponseStatus.SubdocXattrUnknownVattr, typeof(InternalErrorException))]
        [InlineData( ResponseStatus.SubdocXattrCantModifyVattr, typeof(InternalErrorException))]
        [InlineData(ResponseStatus.SubdocMultiPathFailureDeleted, typeof(InternalErrorException))]
        [InlineData( ResponseStatus.SubdocInvalidXattrOrder, typeof(InternalErrorException))]
        //[InlineData(ResponseStatus.CasMismatch)] TODO
       // [InlineData(ResponseStatus.KeyDeleted)] TODO
        public async Task Get_Fails_Throw_KeyValueException(ResponseStatus responseStatus, Type exceptionType)
        {
            var bucket = new FakeBucket(responseStatus);
            var collection = new CouchbaseCollection(bucket, 0, "_default");

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
            var mockBucket = new Mock<FakeBucket>();
            var collection = new CouchbaseCollection(mockBucket.Object, 0, "_default");

            var set = collection.Set<dynamic>("theDocId");
            Assert.NotNull(set);
        }

        [Fact]
        public void Queue_Factory_Test()
        {
            var mockBucket = new Mock<FakeBucket>();
            var collection = new CouchbaseCollection(mockBucket.Object, 0, "_default");

            var queue = collection.Queue<dynamic>("theDocId");
            Assert.NotNull(queue);
        }

        [Fact]
        public void List_Factory_Test()
        {
            var mockBucket = new Mock<FakeBucket>();
            var collection = new CouchbaseCollection(mockBucket.Object, 0, "_default");

            var list = collection.List<dynamic>("theDocId");
            Assert.NotNull(list);
        }

        [Fact]
        public void Dictionary_Factory_Test()
        {
            var mockBucket = new Mock<FakeBucket>();
            var collection = new CouchbaseCollection(mockBucket.Object, 0, "_default");

            var dict = collection.Dictionary<string, dynamic>("theDocId");
            Assert.NotNull(dict);
        }

        internal class FakeBucket : BucketBase
        {
            private Queue<ResponseStatus> _statuses = new Queue<ResponseStatus>();
            public FakeBucket(params ResponseStatus[] statuses)
            {
                foreach (var responseStatuse in statuses)
                {
                    _statuses.Enqueue(responseStatuse);
                }
            }

            public override IViewManager ViewIndexes => throw new NotImplementedException();

            public override ICollectionManager Collections => throw new NotImplementedException();

            internal override Task Send(IOperation op, TaskCompletionSource<IMemoryOwner<byte>> tcs)
            {
                if(_statuses.TryDequeue(out ResponseStatus status))
                {
                    op.Completed(new SocketAsyncState
                    {
                        Status = status
                    });
                }
                else
                {
                    throw new InvalidOperationException();
                }

                return Task.CompletedTask;
            }

            public override Task<IScope> this[string name] => throw new NotImplementedException();

            public override Task<IViewResult<T>> ViewQueryAsync<T>(string designDocument, string viewName, ViewOptions options = null)
            {
                throw new NotImplementedException();
            }

            protected override void LoadManifest()
            {
                throw new NotImplementedException();
            }

            internal override Task Bootstrap(params IClusterNode[] bootstrapNodes)
            {
                throw new NotImplementedException();
            }

            internal override void ConfigUpdated(object sender, BucketConfigEventArgs e)
            {
                throw new NotImplementedException();
            }
        }
    }
}
