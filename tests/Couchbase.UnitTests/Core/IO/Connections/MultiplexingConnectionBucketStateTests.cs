using System;
using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.UnitTests.Helpers;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Connections
{
    // NCBC-4373: the KV error map marks NO_BUCKET (0x08) as conn-state-invalidated, so a connection that
    // receives it after selecting a bucket must be dropped. Before, the SDK kept using it and every operation
    // got NO_BUCKET until the application restarted.
    public class MultiplexingConnectionBucketStateTests
    {
        [Fact]
        public async Task NoBucket_AfterSelectingABucket_ClosesTheConnection()
        {
            // Arrange

            using var stream = new FeedStream();
            var connection = CreateConnection(stream);

            // Act

            stream.Feed(Response(OpCode.SelectBucket, ResponseStatus.Success));
            stream.Feed(Response(OpCode.Get, ResponseStatus.BucketNotConnected));

            // Assert

            // Closing disposes the stream, so this completes only if the connection was closed.
            await stream.Disposed;
            Assert.True(connection.IsDead);
        }

        [Fact]
        public async Task NoBucket_WithoutSelectingABucket_KeepsTheConnection()
        {
            // Arrange

            using var stream = new FeedStream();
            var connection = CreateConnection(stream);
            var marker = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            connection.OnClusterMapChangeNotification = response =>
            {
                response.Dispose();
                marker.TrySetResult(true);
            };

            // Act

            // A cluster-level connection has no bucket, so NO_BUCKET is an ordinary error response here.
            stream.Feed(Response(OpCode.Get, ResponseStatus.BucketNotConnected));

            // The receive loop handles packets in order: once the marker arrives, NO_BUCKET has been handled.
            stream.Feed(ServerPush());
            await marker.Task;

            // Assert

            Assert.False(connection.IsDead);
            connection.Dispose();
        }

        [Fact]
        public async Task FailedSelect_ThenNoBucket_KeepsTheConnection()
        {
            // Arrange

            using var stream = new FeedStream();
            var connection = CreateConnection(stream);
            var marker = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            connection.OnClusterMapChangeNotification = response =>
            {
                response.Dispose();
                marker.TrySetResult(true);
            };

            // Act

            stream.Feed(Response(OpCode.SelectBucket, ResponseStatus.KeyNotFound));
            stream.Feed(Response(OpCode.Get, ResponseStatus.BucketNotConnected));
            stream.Feed(ServerPush());
            await marker.Task;

            // Assert

            Assert.False(connection.IsDead);
            connection.Dispose();
        }

        // The close is graceful: the connection takes no new sends at once, but operations already in flight still
        // get their responses. An immediate Close() would lose them.
        [Fact]
        public async Task NoBucket_AfterSelectingABucket_DrainsTheOperationsInFlightBeforeClosing()
        {
            // Arrange

            using var stream = new FeedStream();
            var connection = CreateConnection(stream);
            var first = new Noop();
            var second = new Noop();
            await connection.SendAsync(new byte[HeaderOffsets.HeaderLength], first);
            await connection.SendAsync(new byte[HeaderOffsets.HeaderLength], second);

            // Act

            stream.Feed(Response(OpCode.SelectBucket, ResponseStatus.Success));
            stream.Feed(Response(OpCode.NoOp, ResponseStatus.BucketNotConnected, first.Opaque));

            // Assert

            Assert.Equal(ResponseStatus.BucketNotConnected, await first.Completed);

            // Marked dead at once, so nothing new is sent on it.
            await Assert.ThrowsAsync<SocketNotAvailableException>(() =>
                connection.SendAsync(new byte[HeaderOffsets.HeaderLength], new Noop()).AsTask());

            // Still open: the close waits for the second operation, which is in flight.
            Assert.False(stream.Disposed.IsCompleted);

            stream.Feed(Response(OpCode.NoOp, ResponseStatus.Success, second.Opaque));
            Assert.Equal(ResponseStatus.Success, await second.Completed);
            await stream.Disposed;
        }

        #region Helpers

        private static MultiplexingConnection CreateConnection(Stream stream) =>
            new(stream, 8, new IPEndPoint(0, 0), new IPEndPoint(0, 0),
                new Logger<MultiplexingConnection>(new LoggerFactory()));

        // A 24-byte response header with no body; the opaque matches no in-flight operation.
        private static byte[] Response(OpCode opCode, ResponseStatus status, uint opaque)
        {
            var packet = Response(opCode, status);
            // The SDK writes the opaque little-endian and the server echoes it unchanged.
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(HeaderOffsets.Opaque), opaque);
            return packet;
        }

        private static byte[] Response(OpCode opCode, ResponseStatus status)
        {
            var packet = new byte[HeaderOffsets.HeaderLength];
            packet[HeaderOffsets.Magic] = (byte) Magic.ClientResponse;
            packet[HeaderOffsets.Opcode] = (byte) opCode;
            packet[HeaderOffsets.Status] = (byte) ((ushort) status >> 8);
            packet[HeaderOffsets.Status + 1] = (byte) status;
            packet[HeaderOffsets.Opaque + 3] = 42;
            return packet;
        }

        // A server-initiated request with no body, which the connection hands to OnClusterMapChangeNotification.
        private static byte[] ServerPush()
        {
            var packet = new byte[HeaderOffsets.HeaderLength];
            packet[HeaderOffsets.Magic] = (byte) Magic.ServerRequest;
            packet[HeaderOffsets.Opcode] = 0x01;
            return packet;
        }

        #endregion
    }
}
