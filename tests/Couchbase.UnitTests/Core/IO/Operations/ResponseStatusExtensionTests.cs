using Couchbase.Core.IO.Operations;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Operations
{
    public class ResponseStatusExtensionTests
    {
        [Theory]
        [InlineData(ResponseStatus.Success)]
        [InlineData(ResponseStatus.RangeScanComplete)]
        [InlineData(ResponseStatus.RangeScanMore)]
        //[InlineData(ResponseStatus.KeyNotFound)]
        public void TestFailureFalse(ResponseStatus status)
        {
            Assert.False(status.Failure());
        }

        //[Theory]
       // [InlineData(ResponseStatus.KeyNotFound)]
        public void TestFailureTrue(ResponseStatus status)
        {
            Assert.True(status.Failure());
        }
    }
}
