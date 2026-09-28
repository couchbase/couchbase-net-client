using System.IO;
using System.Threading.Tasks;

namespace Couchbase.UnitTests.Utils
{
    /// <summary>
    /// Stands in for <c>Stream.ReadExactly</c> and <c>Stream.ReadExactlyAsync</c>, which only exist on
    /// net7.0 and later.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A bare <c>Read</c> is not equivalent: it may return fewer bytes than asked for, which is what
    /// CA2022 flags. Test resources are small enough that it almost always returns everything, so the
    /// difference would show up as a rare, confusing failure rather than an obvious one.
    /// </para>
    /// <para>
    /// Compiled on every target framework, not just the ones missing the instance methods. Call sites
    /// are unaffected either way - an instance method beats an extension method in overload resolution,
    /// so on net8.0 and net10.0 they still bind to the BCL. Compiling it everywhere is what lets
    /// <c>StreamExtensionsTests</c> exercise the loop on every framework rather than only on net48,
    /// which matters because no real call site here ever reads short.
    /// </para>
    /// </remarks>
    internal static class StreamExtensions
    {
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
