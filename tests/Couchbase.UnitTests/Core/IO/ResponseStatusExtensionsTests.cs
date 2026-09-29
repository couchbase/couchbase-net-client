using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Operations;
using Couchbase.KeyValue;
using Xunit;

namespace Couchbase.UnitTests.Core.IO
{
    public class ResponseStatusExtensionsTests
    {
        [Fact]
        public void CreateException_KeyNotFound_On_Index_ReplicaRead_Is_DocumentNotFound()
        {
            using var op = new ReplicaRead<byte[]>("key", 1);

            var exception = ResponseStatus.KeyNotFound.CreateException(op, "default");

            Assert.IsType<DocumentNotFoundException>(exception);
        }

        [Fact]
        public void CreateException_KeyNotFound_On_Strategy_ReplicaRead_Is_DocumentNotFoundOnReplica()
        {
            using var op = new ReplicaRead<byte[]>("key", GetReplicaStrategy.FromIndex(ReplicaIndex.First));

            var exception = ResponseStatus.KeyNotFound.CreateException(op, "default");

            Assert.IsType<DocumentNotFoundOnReplicaException>(exception);
        }

        [Fact]
        public void CreateException_KeyNotFound_On_Get_Is_DocumentNotFound()
        {
            using var op = new Get<byte[]> { Key = "key" };

            var exception = ResponseStatus.KeyNotFound.CreateException(op, "default");

            Assert.IsType<DocumentNotFoundException>(exception);
        }

        [Fact]
        public void CreateException_Attaches_A_KeyValueErrorContext()
        {
            using var op = new ReplicaRead<byte[]>("key", GetReplicaStrategy.FromIndex(ReplicaIndex.First));

            var exception = (DocumentNotFoundOnReplicaException) ResponseStatus.KeyNotFound.CreateException(op, "default");

            var context = Assert.IsType<KeyValueErrorContext>(exception.Context);
            Assert.Equal("key", context.DocumentKey);
            Assert.Equal("default", context.BucketName);
            Assert.Equal(ResponseStatus.KeyNotFound, context.Status);
        }
    }
}
