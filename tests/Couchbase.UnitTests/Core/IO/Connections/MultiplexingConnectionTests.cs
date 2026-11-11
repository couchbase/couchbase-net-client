using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.RangeScan;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Connections
{
    public class SslConnectionTests
    {
        [Fact]
        public async Task When_Packet_Exceeds_MaxDocSize_ThrowValueTooLargeException()
        {
            var conn = new SslConnection(new SslStream(new MemoryStream()), 8, new IPEndPoint(0, 0), new IPEndPoint(0, 0),
                new Logger<SslConnection>(new LoggerFactory()),
                new Logger<MultiplexingConnection>(new LoggerFactory()));

            var json = JsonConvert.SerializeObject(new string[1024 * 6145]);
            var bytes = Encoding.UTF8.GetBytes(json);

            await Assert.ThrowsAsync<ValueToolargeException>(() =>
                conn.SendAsync(bytes, Mock.Of<IOperation>()).AsTask());
        }

        [Fact]
        public void WhenSubscribedCanRemove()
        {
            var conn = new MultiplexingConnection(new SslStream(new MemoryStream()), 8, new IPEndPoint(0, 0), new IPEndPoint(0, 0),
                new Logger<MultiplexingConnection>(new LoggerFactory()));

            var rangeScanOp = new RangeScanContinue();
           // rangeScanOp.Subscribe(conn);
           // rangeScanOp.Unsubscribe();


        }

        // A connection that has entered its graceful-close window (_closing > 0) is already
        // unusable — SendAsync throws SocketNotAvailableException — so the rest of the SDK
        // must observe IsDead=true immediately, not only after the timeout elapses and
        // _disposed is set. This is the contract connection pools rely on to evict and
        // replace half-dead connections promptly instead of waiting up to 60s.
        [Fact]
        public void IsDead_ReturnsTrue_WhenClosingFlagIsSet()
        {
            using var stream = new BlockingStream();
            var conn = new MultiplexingConnection(stream, 8,
                new IPEndPoint(0, 0), new IPEndPoint(0, 0),
                new Logger<MultiplexingConnection>(new LoggerFactory()));

            Assert.False(conn.IsDead);

            // Simulate the state established synchronously at the top of CloseAsync(TimeSpan)
            // via Interlocked.Exchange(ref _closing, 1).
            var closingField = typeof(MultiplexingConnection)
                .GetField("_closing", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(closingField);
            closingField!.SetValue(conn, 1);

            Assert.True(conn.IsDead);
        }

        // Regression: the pre-existing "disposed" path must still flip IsDead to true.
        [Fact]
        public void IsDead_ReturnsTrue_WhenDisposedFlagIsSet()
        {
            using var stream = new BlockingStream();
            var conn = new MultiplexingConnection(stream, 8,
                new IPEndPoint(0, 0), new IPEndPoint(0, 0),
                new Logger<MultiplexingConnection>(new LoggerFactory()));

            Assert.False(conn.IsDead);

            var disposedField = typeof(MultiplexingConnection)
                .GetField("_disposed", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(disposedField);
            disposedField!.SetValue(conn, 1);

            Assert.True(conn.IsDead);
        }

        // NCBC-4236: connections were tracked for the "connections" gauge in a static ConcurrentBag which
        // had no removal, so every connection ever created leaked an entry and a weak GC handle. The pool
        // scale controller cycles idle connections every 30s, so this accumulated continuously. Closing a
        // connection must now untrack it.
        [Fact]
        public void Close_UntracksTheConnectionForDiagnostics()
        {
            using var stream = new BlockingStream();
            var conn = new MultiplexingConnection(stream, 8,
                new IPEndPoint(0, 0), new IPEndPoint(0, 0),
                new Logger<MultiplexingConnection>(new LoggerFactory()));

            Assert.True(conn.IsTrackedForDiagnostics);

            conn.Close();

            Assert.False(conn.IsTrackedForDiagnostics);
        }

        // CloseAsync funnels through Close, and Close may also be called again afterwards. Neither path
        // may resurrect the entry or throw.
        [Fact]
        public async Task CloseAsync_ThenClose_LeavesTheConnectionUntracked()
        {
            using var stream = new BlockingStream();
            var conn = new MultiplexingConnection(stream, 8,
                new IPEndPoint(0, 0), new IPEndPoint(0, 0),
                new Logger<MultiplexingConnection>(new LoggerFactory()));

            Assert.True(conn.IsTrackedForDiagnostics);

            await conn.CloseAsync(TimeSpan.Zero);
            Assert.False(conn.IsTrackedForDiagnostics);

            conn.Close();
            Assert.False(conn.IsTrackedForDiagnostics);
        }

        #region Close while sends wait

        // NCBC-4328 (A), fixed under NCBC-4372: closing a connection while a send waits for an in-flight slot left
        // that send, and so the pool's processor awaiting it, waiting forever.
        [Fact]
        public async Task SendAsync_WaitingForASlot_FailsWhenTheConnectionCloses()
        {
            // Arrange

            using var stream = new BlockingStream();
            var conn = new MultiplexingConnection(stream, 1, new IPEndPoint(0, 0), new IPEndPoint(0, 0),
                new Logger<MultiplexingConnection>(new LoggerFactory()));

            // The first send takes the only slot; nothing ever answers it.
            await conn.SendAsync(new byte[24], Mock.Of<IOperation>(op => op.Opaque == 1));
            var waiting = conn.SendAsync(new byte[24], Mock.Of<IOperation>(op => op.Opaque == 2)).AsTask();
            Assert.False(waiting.IsCompleted);

            // Act

            conn.Close();

            // Assert

            // Retriable: the second operation was never sent.
            await Assert.ThrowsAsync<SocketNotAvailableException>(() => waiting);
        }

        #endregion

        #region Cancellation before the write

        // A hung node holds every slot. The token lets a send give up at its timeout instead of the in-flight cleanup.
        [Fact]
        public async Task SendAsync_WaitingForASlot_ThrowsWhenTheTokenFires()
        {
            // Arrange

            using var stream = new BlockingStream();
            var conn = new MultiplexingConnection(stream, 1, new IPEndPoint(0, 0), new IPEndPoint(0, 0),
                new Logger<MultiplexingConnection>(new LoggerFactory()));

            // The first send takes the only slot. Nothing answers it.
            await conn.SendAsync(new byte[24], Mock.Of<IOperation>(op => op.Opaque == 1));
            using var cts = new CancellationTokenSource();
            var waiting = conn.SendAsync(new byte[24], Mock.Of<IOperation>(op => op.Opaque == 2), cts.Token).AsTask();
            Assert.False(waiting.IsCompleted);
            Assert.Equal(1, InFlightCount(conn));

            // Act

            cts.Cancel();

            // Assert

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(1, InFlightCount(conn));
            Assert.False(conn.IsDead);
        }

        [Fact]
        public async Task SendAsync_WaitingForTheWriteLock_ThrowsWhenTheTokenFires_AndFreesItsSlot()
        {
            // Arrange

            using var stream = new GatedWriteStream();
            var conn = CreateConnection(stream);
            var first = Enumerable.Repeat((byte) 1, 24).ToArray();

            var writing = conn.SendAsync(first, Mock.Of<IOperation>(op => op.Opaque == 1)).AsTask();
            await stream.WriteStarted.WaitAsync(TimeSpan.FromSeconds(10));
            using var cts = new CancellationTokenSource();
            var waiting = conn.SendAsync(Enumerable.Repeat((byte) 2, 24).ToArray(),
                Mock.Of<IOperation>(op => op.Opaque == 2), cts.Token).AsTask();
            Assert.False(waiting.IsCompleted);
            Assert.Equal(2, InFlightCount(conn));

            // Act

            cts.Cancel();

            // Assert

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(1, InFlightCount(conn));

            stream.ReleaseWrites();
            await writing.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(first, stream.Written);
            Assert.False(conn.IsDead);
        }

        [Fact]
        public async Task SendAsync_WriteLockGrantedAfterTheTokenFires_ThrowsAndKeepsTheConnection()
        {
            // Arrange

            using var stream = new GatedWriteStream();
            var conn = CreateConnection(stream);
            var writeMutex = (SemaphoreSlim) typeof(MultiplexingConnection)
                .GetField("_writeMutex", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(conn)!;

            // Another write holds the lock.
            writeMutex.Wait();
            using var cts = new CancellationTokenSource();
            var waiting = conn.SendAsync(new byte[24], Mock.Of<IOperation>(op => op.Opaque == 1), cts.Token).AsTask();
            Assert.False(waiting.IsCompleted);

            // Token callbacks run newest first, so the lock goes to the send before its wait sees the token.
            cts.Token.Register(() => writeMutex.Release());

            // Act

            cts.Cancel();

            // Assert

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.False(conn.IsDead);
            Assert.Empty(stream.Written);
            Assert.Equal(0, InFlightCount(conn));
            Assert.Equal(1, writeMutex.CurrentCount);
        }

        // A cut write leaves part of a frame on the stream, so the connection must close before the node reads past it.
        [Fact]
        public async Task SendAsync_TokenFiresDuringTheWrite_ThrowsAndClosesTheConnection()
        {
            // Arrange

            using var stream = new GatedWriteStream();
            var conn = CreateConnection(stream);

            using var cts = new CancellationTokenSource();
            var sending = conn.SendAsync(new byte[24], Mock.Of<IOperation>(op => op.Opaque == 1), cts.Token).AsTask();
            await stream.WriteStarted.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(sending.IsCompleted);

            // Act

            cts.Cancel();

            // Assert

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sending.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.True(conn.IsDead);
        }

        private static int InFlightCount(MultiplexingConnection connection) =>
            ((InFlightOperationSet) typeof(MultiplexingConnection)
                .GetField("_statesInFlight", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(connection)!).Count;

        #endregion

        #region GetConnectionCount

        // Each connection gets its own stream throughout this region. Closing a stream makes the owning
        // connection's receive loop fail, and HandleDisconnect responds by calling Close() on that
        // connection; sharing one stream would therefore let one test connection asynchronously kill
        // another and make the counts racy.
        [Fact]
        public void GetConnectionCount_CountsLiveConnections()
        {
            using var firstStream = new BlockingStream();
            using var secondStream = new BlockingStream();
            var registry = new WeakInstanceRegistry<MultiplexingConnection>();
            var first = CreateConnection(firstStream);
            var second = CreateConnection(secondStream);

            registry.Add(first);
            registry.Add(second);

            Assert.Equal(2, MultiplexingConnection.GetConnectionCount(registry));

            GC.KeepAlive(first);
            GC.KeepAlive(second);
        }

        [Fact]
        public void GetConnectionCount_IsZero_WhenNoConnectionsAreTracked()
        {
            var registry = new WeakInstanceRegistry<MultiplexingConnection>();

            Assert.Equal(0, MultiplexingConnection.GetConnectionCount(registry));
        }

        // The gauge reports *active* connections, so a connection which has been closed must stop counting
        // even while its entry is still present. This is the IsDead filter, and it is the part most likely
        // to be lost in a future refactor of the registry.
        [Fact]
        public void GetConnectionCount_ExcludesClosedConnections()
        {
            using var liveStream = new BlockingStream();
            using var closedStream = new BlockingStream();
            var registry = new WeakInstanceRegistry<MultiplexingConnection>();
            var live = CreateConnection(liveStream);
            var closed = CreateConnection(closedStream);

            registry.Add(live);
            registry.Add(closed);

            Assert.Equal(2, MultiplexingConnection.GetConnectionCount(registry));

            closed.Close();

            Assert.Equal(1, MultiplexingConnection.GetConnectionCount(registry));

            GC.KeepAlive(live);
            GC.KeepAlive(closed);
        }

        // A connection abandoned without being closed leaves a dead weak reference behind. Reading the
        // gauge must skip it rather than throwing.
        [Fact]
        public void GetConnectionCount_SkipsCollectedConnections()
        {
            using var stream = new BlockingStream();
            var registry = new WeakInstanceRegistry<MultiplexingConnection>();
            var live = CreateConnection(stream);

            registry.AddWeak(new WeakReference<MultiplexingConnection>(null!));
            registry.Add(live);
            registry.AddWeak(new WeakReference<MultiplexingConnection>(null!));

            Assert.Equal(1, MultiplexingConnection.GetConnectionCount(registry));

            GC.KeepAlive(live);
        }

        private static MultiplexingConnection CreateConnection(Stream stream) =>
            new(stream, 8, new IPEndPoint(0, 0), new IPEndPoint(0, 0),
                new Logger<MultiplexingConnection>(new LoggerFactory()));

        #endregion

        // A Stream whose ReadAsync blocks indefinitely, so the MultiplexingConnection's
        // fire-and-forget receive loop doesn't tear the connection down during the test.
        private class BlockingStream : Stream
        {
            private readonly System.Threading.CancellationTokenSource _cts = new();
            private bool _disposed;
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => 0;
            public override long Position { get => 0; set { } }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => throw new System.NotSupportedException();
            public override async System.Threading.Tasks.Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
            {
                using var linked = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
                await System.Threading.Tasks.Task.Delay(System.Threading.Timeout.Infinite, linked.Token).ConfigureAwait(false);
                return 0;
            }
#if !NETFRAMEWORK
            public override async System.Threading.Tasks.ValueTask<int> ReadAsync(System.Memory<byte> buffer, System.Threading.CancellationToken cancellationToken = default)
            {
                using var linked = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
                await System.Threading.Tasks.Task.Delay(System.Threading.Timeout.Infinite, linked.Token).ConfigureAwait(false);
                return 0;
            }
#endif
            public override long Seek(long offset, SeekOrigin origin) => throw new System.NotSupportedException();
            public override void SetLength(long value) { }
            public override void Write(byte[] buffer, int offset, int count) { }
            protected override void Dispose(bool disposing)
            {
                // MultiplexingConnection.Close disposes the stream, and the test's `using` then disposes
                // it again. Stream.Dispose is required to be idempotent, so guard the CancellationTokenSource.
                if (disposing && !_disposed)
                {
                    _disposed = true;
                    _cts.Cancel();
                    _cts.Dispose();
                }
                base.Dispose(disposing);
            }
        }

        // Writes wait for ReleaseWrites. Like a socket, a write gives up if its token fires.
        private sealed class GatedWriteStream : BlockingStream
        {
            private readonly TaskCompletionSource<bool> _writeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<bool> _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly MemoryStream _written = new();

            public Task WriteStarted => _writeStarted.Task;

            public byte[] Written
            {
                get
                {
                    lock (_written)
                    {
                        return _written.ToArray();
                    }
                }
            }

            public void ReleaseWrites() => _gate.TrySetResult(true);

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
                WriteCoreAsync(new ArraySegment<byte>(buffer, offset, count).ToArray(), cancellationToken);

#if !NETFRAMEWORK
            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
                new(WriteCoreAsync(buffer.ToArray(), cancellationToken));
#endif

            private async Task WriteCoreAsync(byte[] data, CancellationToken cancellationToken)
            {
                _writeStarted.TrySetResult(true);

                var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using (cancellationToken.Register(static state => ((TaskCompletionSource<bool>) state!).TrySetResult(true), cancelled))
                {
                    await Task.WhenAny(_gate.Task, cancelled.Task).ConfigureAwait(false);
                }

                cancellationToken.ThrowIfCancellationRequested();
                lock (_written)
                {
                    _written.Write(data, 0, data.Length);
                }
            }

            protected override void Dispose(bool disposing)
            {
                _gate.TrySetResult(true);
                base.Dispose(disposing);
            }
        }
    }
}
