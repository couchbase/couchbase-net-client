using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Legacy;
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

            Assert.ThrowsAsync<TimeoutException>(async () => await collection.Get("key", options =>
            {
                options.WithTimeout(TimeSpan.FromMilliseconds(1d));
            }));
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
                using (await collection.Get("key"))
                {
                }
            }
            catch (Exception e)
            {
                Assert.IsType(exceptionType, e);
            }
        }

        public class FakeBucket : IBucket, IBucketSender
        {
            private Queue<ResponseStatus> _statuses = new Queue<ResponseStatus>();
            public FakeBucket(params ResponseStatus[] statuses)
            {
                foreach (var responseStatuse in statuses)
                {
                    _statuses.Enqueue(responseStatuse);
                }
            }
            public virtual void Dispose()
            {

            }
            public virtual string Name { get; }

            public Task BootstrapAsync(Uri uri, IConfiguration configuration)
            {
                throw new NotImplementedException();
            }

            public Task<IScope> this[string name] => throw new NotImplementedException();

            public Task<ICollection> DefaultCollection => throw new NotImplementedException();

            public Task<IScope> Scope(string name)
            {
                throw new NotImplementedException();
            }

            public Task<IViewResult<T>> ViewQueryAsync<T>(string designDocument, string viewName, ViewOptions options)
            {
                throw new NotImplementedException();
            }

            public Task<IViewResult<T>> ViewQueryAsync<T>(string designDocument, string viewName, Action<ViewOptions> configureOptions)
            {
                throw new NotImplementedException();
            }

            public Task<IViewResult<T>> SpatialViewQuery<T>(string designDocument, string viewName, SpatialViewOptions options)
            {
                throw new NotImplementedException();
            }

            public Task<IViewResult<T>> SpatialViewQuery<T>(string designDocument, string viewName, Action<SpatialViewOptions> configureOptions)
            {
                throw new NotImplementedException();
            }

            public Task Send(IOperation op, TaskCompletionSource<IMemoryOwner<byte>> tcs)
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
        }
    }
}
