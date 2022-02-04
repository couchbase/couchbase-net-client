using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Operations;
using Xunit;

namespace Couchbase.UnitTests.Core.Exceptions.KeyValue
{
    public class KeyValueErrorContextTests
    {
        [Fact]
        public void Test_ToString()
        {
            var ctx = new KeyValueErrorContext
            {
                Status = ResponseStatus.Eaccess,
                ScopeName = "scope1",
                BucketName = "bucket1",
                CollectionName = "coll1",
                Cas = 911,
                ClientContextId =   "contextId",
                OpCode = OpCode.SelectBucket,
                DispatchedFrom = "127.0.0.1",
                DocumentKey = "doc1",
                Message = "the message is blah"
            };

            var json = ctx.ToString();
            Assert.Contains("selectBucket", json);
            Assert.Contains("eaccess", json);
        }
    }
}
