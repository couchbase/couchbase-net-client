using System.IO;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.RangeScan;
using Couchbase.Core.IO.Transcoders;
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

        // A Stream whose ReadAsync blocks indefinitely, so the MultiplexingConnection's
        // fire-and-forget receive loop doesn't tear the connection down during the test.
        private sealed class BlockingStream : Stream
        {
            private readonly System.Threading.CancellationTokenSource _cts = new();
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
                if (disposing)
                {
                    _cts.Cancel();
                    _cts.Dispose();
                }
                base.Dispose(disposing);
            }
        }
    }
}
