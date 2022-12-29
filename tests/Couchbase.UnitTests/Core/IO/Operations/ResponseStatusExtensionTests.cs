using Couchbase.Core.IO.Operations;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Operations
{
    public class ResponseStatusExtensionTests
    {
        [Theory]
        [InlineData(ResponseStatus.Success, OpCode.Get)]
        [InlineData(ResponseStatus.RangeScanComplete, OpCode.Get)]
        [InlineData(ResponseStatus.RangeScanMore, OpCode.Get)]
        [InlineData(ResponseStatus.SubDocMultiPathFailure, OpCode.MultiLookup)]
        public void TestFailureFalse(ResponseStatus status, OpCode opCode)
        {
            Assert.False(status.Failure(opCode));
        }

        [Theory]
        [InlineData(ResponseStatus.KeyNotFound, OpCode.Add)]
        public void TestFailureTrue(ResponseStatus status, OpCode opCode)
        {
            Assert.True(status.Failure(opCode));
        }
    }
}
