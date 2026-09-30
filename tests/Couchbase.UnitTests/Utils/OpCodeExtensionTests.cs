using Couchbase.Core.IO.Operations;
using Couchbase.Utils;
using Xunit;

namespace Couchbase.UnitTests.Utils
{
    public class OpCodeExtensionTests
    {
        #region ToMetricTag

        [Theory]
        [InlineData(OpCode.RangeScanCreate, "rangescancreate")]
        [InlineData(OpCode.RangeScanContinue, "rangescancontinue")]
        [InlineData(OpCode.RangeScanCancel, "rangescancancel")]
        public void ToMetricTag_RangeScan_UsesCrossSdkName(OpCode opCode, string expected)
        {
            // Act

            var result = opCode.ToMetricTag();

            // Assert
            // Not the enum name: the default ToString() fallback would give "RangeScanCreate",
            // which no other SDK emits and which the FIT observability tests do not match.

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(OpCode.Get, "get")]
        [InlineData(OpCode.GetL, "get_and_lock")]
        [InlineData(OpCode.MultiLookup, "lookup_in")]
        public void ToMetricTag_MappedOpCode_UsesSpanName(OpCode opCode, string expected)
        {
            // Act

            var result = opCode.ToMetricTag();

            // Assert

            Assert.Equal(expected, result);
        }

        #endregion
    }
}
