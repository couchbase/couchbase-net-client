using System.Buffers;
using System.Text;
using Couchbase.Core.IO.Converters;

namespace Couchbase.Utils
{
    internal static class BufferWriterExtensions
    {
        public static void WriteUtf8String(this IBufferWriter<byte> bufferWriter, string value)
        {
#if NET6_0_OR_GREATER
            Encoding.UTF8.GetBytes(value, bufferWriter);
#else
            var buffer = bufferWriter.GetSpan(ByteConverter.GetStringByteCount(value));
            var length = ByteConverter.FromString(value, buffer);
            bufferWriter.Advance(length);
#endif
        }
    }
}
