using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.IO.Utils;
using NUnit.Framework;

namespace Couchbase.UnitTests.Tracing
{
    [TestFixture]
    public class OperationHeaderExtensionsTests
    {
        [TestCase((ushort) 0, null)]
        [TestCase((ushort) 50, (long) 452)]
        public void Create_Header_and_get_server_duration_from_framing_extras(ushort encoded, long? decoded)
        {
            // first four bits is 0 (type: server duration), last four bits is 2 (length)
            const byte frameInfo = 1 << 1;

            var converter = new DefaultConverter();
            var bytes = new byte[27];
            converter.FromByte((byte) Magic.AltResponse, bytes, HeaderIndexFor.Magic);
            converter.FromByte(3, bytes, HeaderIndexFor.FramingExtras);
            converter.FromByte(frameInfo, bytes, 24);
            converter.FromUInt16(encoded, bytes, 25);

            var header = bytes.CreateHeader();
            Assert.IsNotNull(header);
            Assert.AreEqual(3, header.FramingExtrasLength);

            var serverDuration = header.GetServerDuration(bytes);
            Assert.AreEqual(decoded, serverDuration);
        }
    }
}
