#if !NET8_0_OR_GREATER
using System.IO;
using System.Threading.Tasks;

namespace Couchbase.UnitTests.Utils
{
    internal static class StreamExtensions
    {
        /// <summary>
        /// Stands in for <c>Stream.ReadExactlyAsync</c>, which only exists on net7.0 and later. On the
        /// modern target frameworks the instance method wins overload resolution and this is never
        /// called, so call sites can read exactly without a per-site #if.
        /// </summary>
        /// <remarks>
        /// A bare <c>Read</c> is not equivalent: it may return fewer bytes than asked for, which is what
        /// CA2022 flags. Test resources are small enough that it almost always returns everything, so the
        /// difference would show up as a rare, confusing failure rather than an obvious one.
        /// </remarks>
        public static async Task ReadExactlyAsync(this Stream stream, byte[] buffer, int offset, int count)
        {
            var read = 0;
            while (read < count)
            {
                var n = await stream.ReadAsync(buffer, offset + read, count - read).ConfigureAwait(false);
                if (n == 0)
                {
                    throw new EndOfStreamException(
                        $"Expected {count} bytes but the stream ended after {read}.");
                }

                read += n;
            }
        }

        /// <inheritdoc cref="ReadExactlyAsync"/>
        public static void ReadExactly(this Stream stream, byte[] buffer, int offset, int count)
        {
            var read = 0;
            while (read < count)
            {
                var n = stream.Read(buffer, offset + read, count - read);
                if (n == 0)
                {
                    throw new EndOfStreamException(
                        $"Expected {count} bytes but the stream ended after {read}.");
                }

                read += n;
            }
        }
    }
}
#endif
