using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Couchbase.UnitTests.Utils
{
    /// <summary>
    /// Covers <see cref="StreamExtensions"/>. The 21 production call sites all read from manifest
    /// resource streams, which satisfy a read in one call, so none of them ever takes the loop or the
    /// early-end branch. These do.
    /// </summary>
    public class StreamExtensionsTests
    {
        /// <summary>
        /// Hands back at most <paramref name="chunkSize"/> bytes per read, then reports end of stream.
        /// The point is a stream that returns short reads deterministically, which no test resource does.
        /// </summary>
        private sealed class ChunkedStream : Stream
        {
            private readonly byte[] _content;
            private readonly int _chunkSize;
            private int _position;

            public ChunkedStream(byte[] content, int chunkSize)
            {
                _content = content;
                _chunkSize = chunkSize;
            }

            public int ReadCallCount { get; private set; }

            public override int Read(byte[] buffer, int offset, int count)
            {
                ReadCallCount++;

                var remaining = _content.Length - _position;
                if (remaining <= 0)
                {
                    return 0;
                }

                var take = Math.Min(Math.Min(count, _chunkSize), remaining);
                Array.Copy(_content, _position, buffer, offset, take);
                _position += take;
                return take;
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
                Task.FromResult(Read(buffer, offset, count));

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _content.Length;
            public override long Position { get => _position; set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        private static byte[] Content(int length) => Enumerable.Range(0, length).Select(i => (byte) i).ToArray();

        // Called as static methods, not extension syntax, on purpose. Written as
        // stream.ReadExactly(...) these would bind to the BCL instance method on net8.0+ and
        // silently test the framework instead of the shim - which is exactly what happened on
        // the first run of this file.

        [Theory]
        [InlineData(1)]   // one byte at a time - the loop runs `count` times
        [InlineData(3)]   // uneven chunks, so the final read is partial
        [InlineData(16)]  // satisfied in a single read, the path the real call sites take
        public void ReadExactly_ReassemblesAcrossShortReads(int chunkSize)
        {
            var expected = Content(16);
            var stream = new ChunkedStream(expected, chunkSize);
            var buffer = new byte[expected.Length];

            StreamExtensions.ReadExactly(stream, buffer, 0, buffer.Length);

            Assert.Equal(expected, buffer);
            Assert.Equal((int) Math.Ceiling(16.0 / chunkSize), stream.ReadCallCount);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(16)]
        public async Task ReadExactlyAsync_ReassemblesAcrossShortReads(int chunkSize)
        {
            var expected = Content(16);
            var stream = new ChunkedStream(expected, chunkSize);
            var buffer = new byte[expected.Length];

            await StreamExtensions.ReadExactlyAsync(stream, buffer, 0, buffer.Length);

            Assert.Equal(expected, buffer);
            Assert.Equal((int) Math.Ceiling(16.0 / chunkSize), stream.ReadCallCount);
        }

        [Fact]
        public void ReadExactly_StreamEndsEarly_Throws()
        {
            var stream = new ChunkedStream(Content(4), chunkSize: 1);
            var buffer = new byte[8];

            var ex = Assert.Throws<EndOfStreamException>(
                () => StreamExtensions.ReadExactly(stream, buffer, 0, buffer.Length));

            Assert.Contains("ended after 4", ex.Message);
        }

        [Fact]
        public async Task ReadExactlyAsync_StreamEndsEarly_Throws()
        {
            var stream = new ChunkedStream(Content(4), chunkSize: 1);
            var buffer = new byte[8];

            var ex = await Assert.ThrowsAsync<EndOfStreamException>(
                () => StreamExtensions.ReadExactlyAsync(stream, buffer, 0, buffer.Length));

            Assert.Contains("ended after 4", ex.Message);
        }

        [Fact]
        public void ReadExactly_HonoursTheOffset()
        {
            var stream = new ChunkedStream(Content(4), chunkSize: 1);
            var buffer = new byte[8];

            StreamExtensions.ReadExactly(stream, buffer, 2, 4);

            Assert.Equal(new byte[] { 0, 0, 0, 1, 2, 3, 0, 0 }, buffer);
        }
    }
}
