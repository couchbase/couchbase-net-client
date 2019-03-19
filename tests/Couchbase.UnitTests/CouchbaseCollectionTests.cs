using System;
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
        [InlineData(ResponseStatus.KeyNotFound)]
        [InlineData(ResponseStatus.KeyExists)]
        [InlineData(ResponseStatus.ValueTooLarge)]
        [InlineData(ResponseStatus.Locked)]
        [InlineData(ResponseStatus.TemporaryFailure)]
        [InlineData(ResponseStatus.InvalidRange)]
        //[InlineData(ResponseStatus.CasMismatch)] TODO
       // [InlineData(ResponseStatus.KeyDeleted)] TODO
        public async Task Get_Fails_Throw_KeyValueException(ResponseStatus responseStatus)
        {
            var bucket = new FakeBucket(responseStatus);
            var collection = new CouchbaseCollection(bucket, 0, "_default");

            try
            {
                var result = await collection.Get("key");
            }
            catch (KeyValueException e)
            {
                Assert.Equal(responseStatus, e.ResponseStatus);
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

            public Task<IViewResult> ViewQuery<T>(string statement, IViewOptions options)
            {
                throw new NotImplementedException();
            }

            public Task<ISpatialViewResult> SpatialViewQuery<T>(string statement, ISpatialViewOptions options)
            {
                throw new NotImplementedException();
            }

            public Task Send(IOperation op, TaskCompletionSource<byte[]> tcs)
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
