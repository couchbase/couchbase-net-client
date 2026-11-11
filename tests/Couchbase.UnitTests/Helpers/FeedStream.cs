using System;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Couchbase.UnitTests.Helpers
{
    /// <summary>
    /// A stream whose reads return the packets the test feeds, and which signals when it is disposed.
    /// </summary>
    internal sealed class FeedStream : Stream
    {
        private readonly Channel<byte[]> _packets = Channel.CreateUnbounded<byte[]>();
        private readonly TaskCompletionSource<bool> _disposed =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Disposed => _disposed.Task;

        public void Feed(byte[] packet) => _packets.Writer.TryWrite(packet);

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            byte[] packet;
            try
            {
                packet = await _packets.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (ChannelClosedException)
            {
                return 0;
            }

            // Tests feed bare headers, far smaller than any read buffer the connection uses.
            Buffer.BlockCopy(packet, 0, buffer, offset, packet.Length);
            return packet.Length;
        }

#if !NETFRAMEWORK
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            byte[] packet;
            try
            {
                packet = await _packets.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (ChannelClosedException)
            {
                return 0;
            }

            packet.CopyTo(buffer);
            return packet.Length;
        }
#endif

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _packets.Writer.TryComplete();
                _disposed.TrySetResult(true);
            }

            base.Dispose(disposing);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => 0;
        public override long Position { get => 0; set { } }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) { }
        public override void Write(byte[] buffer, int offset, int count) { }
    }
}
