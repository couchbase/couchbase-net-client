using Couchbase.Core.IO.Operations.Legacy;
using Xunit;

namespace Couchbase.UnitTests
{
    public class FramingExtraInfoTests
    {
        [Theory]
        [InlineData(RequestFramingExtraType.DurabilityRequirements, 1)]
        public void Encodes(RequestFramingExtraType type, byte length)
        {
            var framingExtra = new FramingExtraInfo(type, length);

            Assert.Equal(type, (RequestFramingExtraType) framingExtra.Type);
            Assert.Equal(length, (ushort) framingExtra.Length);
        }
    }
}
