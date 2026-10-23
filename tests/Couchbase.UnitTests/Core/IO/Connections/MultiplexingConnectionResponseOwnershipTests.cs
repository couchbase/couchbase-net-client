using System;
using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Connections
{
    // NCBC-4328 (B): once state.Complete has taken a response's buffer, the receive loop must not dispose it,
    // even if something after Complete throws. It used to, and the queued handler then read a freed buffer and
    // crashed the process.
    public class MultiplexingConnectionResponseOwnershipTests
    {
        private delegate void HandleCompleted(in SlicedMemoryOwner<byte> data);

        [Fact]
        public async Task FailureAfterComplete_LeavesTheBufferWithTheOperation()
        {
            // Arrange

            using var stream = new FeedStream();
            var connection = new MultiplexingConnection(stream, 8, new IPEndPoint(0, 0), new IPEndPoint(0, 0),
                new Logger<MultiplexingConnection>(new LoggerFactory()));

            // The handler waits until the receive loop's failure has been handled, then reads its buffer.
            using var handlerMayRead = new ManualResetEventSlim();
            var readOutcome = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            const uint opaque = 7;
            var operation = new Mock<IOperation>();
            operation.SetupGet(m => m.Opaque).Returns(opaque);
            operation
                .Setup(m => m.HandleOperationCompleted(in It.Ref<SlicedMemoryOwner<byte>>.IsAny))
                .Callback(new HandleCompleted((in SlicedMemoryOwner<byte> data) =>
                {
                    handlerMayRead.Wait();
                    try
                    {
                        readOutcome.TrySetResult($"read magic 0x{data.Memory.Span[HeaderOffsets.Magic]:x2}");
                    }
                    catch (Exception e)
                    {
                        readOutcome.TrySetResult($"buffer unusable: {e.GetType().Name}");
                    }
                }));

            await connection.SendAsync(new byte[HeaderOffsets.HeaderLength], operation.Object);
            connection.AfterStateCompletedForTests = () => throw new InvalidOperationException("after Complete");

            // Act

            stream.Feed(Response(opaque));

            // The failure ends the receive loop and closes the connection, which disposes the stream.
            await stream.Disposed;
            handlerMayRead.Set();

            // Assert

            Assert.Equal($"read magic 0x{(byte) Magic.ClientResponse:x2}", await readOutcome.Task);
        }

        private static byte[] Response(uint opaque)
        {
            var packet = new byte[HeaderOffsets.HeaderLength];
            packet[HeaderOffsets.Magic] = (byte) Magic.ClientResponse;
            packet[HeaderOffsets.Opcode] = (byte) OpCode.NoOp;
            // The SDK writes the opaque little-endian and the server echoes it unchanged.
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(HeaderOffsets.Opaque), opaque);
            return packet;
        }

        /// <summary>
        /// A stream whose reads return the packets the test feeds, and which signals when it is disposed.
        /// </summary>
        private sealed class FeedStream : Stream
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
}
