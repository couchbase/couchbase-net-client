#nullable enable
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Utils;

namespace Couchbase.Test.Common.Configuration
{
    /// <summary>
    /// The in-memory network of <see cref="ConnectionMode.Real"/>.
    /// </summary>
    internal sealed class NetworkOptions
    {
        /// <summary>
        /// Seeds the chunk sizes. Each connection gets its own sequences, from the seed and the order it opened in.
        /// </summary>
        public int Seed { get; set; }

        /// <summary>
        /// Splits every write into chunks of random size and caps every read at one chunk, in both directions.
        /// So the SDK receive loop and the node both read partial frames. On by default.
        /// </summary>
        public bool Fragment { get; set; } = true;

        /// <summary>
        /// How many bytes each direction of a connection holds before its writer waits, like a TCP window.
        /// </summary>
        public int BufferSize { get; set; } = 64 * 1024;
    }

    internal sealed partial class FakeClusterServer
    {
        private static readonly TimeSpan StopLimit = TimeSpan.FromSeconds(5);

        private readonly NetworkOptions? _network;
        private readonly List<Task> _sessionLoops = new();
        private int _streamsOpened;
        private int _requestsInProcess;
        private long _networkActivity;
        private volatile bool _networkShutDown;

        /// <summary>
        /// True when the SDK reaches this server over in-memory streams.
        /// </summary>
        public bool HasNetwork => _network is not null;

        /// <summary>
        /// Changes each time bytes, requests or connections move on the network. Always 0 in fake mode.
        /// </summary>
        internal long NetworkActivity => Interlocked.Read(ref _networkActivity);

        /// <summary>
        /// True while bytes or requests are on their way. Bytes that a stalled host does not read, and the replies and
        /// notifications that a hung host holds, do not count.
        /// </summary>
        internal bool IsNetworkBusy => Volatile.Read(ref _requestsInProcess) > 0 || OpenStreams().Any(s => s.IsBusy);

        /// <summary>
        /// The host reads requests and holds every reply and notification, like a paused process.
        /// New connections to it open and then hang the same way.
        /// </summary>
        public void Hang(string host)
        {
            var state = NetworkHost(host);
            lock (state.Lock) state.Hung = true;
        }

        /// <summary>
        /// The host stops reading. An SDK write waits once the buffer to the host is full.
        /// </summary>
        public void Stall(string host)
        {
            var state = NetworkHost(host);
            lock (state.Lock) state.Stall();
        }

        /// <summary>
        /// Ends <see cref="Hang"/> and <see cref="Stall"/>. Each connection answers the requests it holds and sends the
        /// notifications it holds, in the order they came.
        /// </summary>
        public void Resume(string host)
        {
            var state = NetworkHost(host);
            lock (state.Lock)
            {
                state.Hung = false;
                state.Unstall();
                foreach (var session in state.Sessions.OfType<StreamSession>())
                {
                    session.ReleaseHeld();
                }
            }
        }

        /// <summary>
        /// The host ends every connection to it. The SDK reads what the host sent before, then the end of the stream.
        /// The replies and notifications the host held are lost.
        /// </summary>
        public void Close(string host) => CloseStreams(NetworkHost(host));

        private static void CloseStreams(HostState state)
        {
            List<StreamSession> sessions;
            lock (state.Lock) sessions = state.Sessions.OfType<StreamSession>().ToList();
            foreach (var session in sessions)
            {
                session.Close();
            }
        }

        /// <summary>
        /// While on, a connect to the host fails like a refused TCP connect. Open connections stay open.
        /// </summary>
        public void Refuse(string host, bool refuse)
        {
            var state = NetworkHost(host);
            lock (state.Lock) state.Refused = refuse;
        }

        /// <summary>
        /// Opens a connection to the node at the endpoint and returns the SDK end of it. Read <see cref="Route"/>.
        /// </summary>
        /// <exception cref="SocketException">The node refuses connections, or the network is shut down.</exception>
        internal ClientStream ConnectStream(HostEndpointWithPort endPoint)
        {
            var options = _network ?? throw new InvalidOperationException("This server answers inside each send. Call Connect.");
            var (host, seed) = Route(endPoint);
            var state = HostFor(host);
            StreamSession session;
            lock (state.Lock)
            {
                if (state.Refused || _networkShutDown)
                {
                    Interlocked.Increment(ref state.ConnectionsRefused);
                    throw new SocketException((int)SocketError.ConnectionRefused);
                }

                session = new StreamSession(this, state, endPoint, seed, options, Interlocked.Increment(ref _streamsOpened));
                state.Sessions.Add(session);
            }

            lock (_lock)
            {
                _sessions.Add(session);
                _sessionLoops.Add(session.Start());
            }
            Interlocked.Increment(ref state.ConnectionsOpened);
            Moved();
            return session.Client;
        }

        /// <summary>
        /// Refuses new connections and ends every open one, so nothing in the SDK waits on the network.
        /// </summary>
        internal void ShutDownNetwork()
        {
            _networkShutDown = true;
            List<HostState> hosts;
            lock (_lock) hosts = _hosts.Values.ToList();
            foreach (var state in hosts)
            {
                CloseStreams(state);
            }
        }

        /// <summary>
        /// Waits until the read and write loop of every connection has ended.
        /// </summary>
        /// <exception cref="TimeoutException">A loop still runs after a few seconds.</exception>
        internal async Task WhenNetworkStoppedAsync()
        {
            Task[] loops;
            lock (_lock) loops = _sessionLoops.ToArray();
            var all = Task.WhenAll(loops);
            using var cts = new CancellationTokenSource();
            var completed = await Task.WhenAny(all, Task.Delay(StopLimit, cts.Token)).ConfigureAwait(false);
            cts.Cancel();
            if (completed != all)
            {
                throw new TimeoutException(
                    $"{loops.Count(t => !t.IsCompleted)} of {loops.Length} connection loops still run {StopLimit.TotalSeconds} s after the network shut down.");
            }
            await all.ConfigureAwait(false);
        }

        /// <summary>
        /// The open connections that have bytes or requests on their way, for a test that does not settle.
        /// </summary>
        internal string BusyStreams() =>
            string.Join(", ", OpenStreams().Where(s => s.IsBusy).Select(s => s.Traffic()));

        private List<StreamSession> OpenStreams()
        {
            lock (_lock) return _sessions.OfType<StreamSession>().Where(s => !s.IsClosed).ToList();
        }

        private HostState NetworkHost(string host)
        {
            if (_network is null)
            {
                throw new InvalidOperationException("Network controls need a harness in ConnectionMode.Real.");
            }
            return HostFor(host);
        }

        private void Moved(long amount = 1) => Interlocked.Add(ref _networkActivity, amount);

        private void OnStreamClosed(StreamSession session)
        {
            lock (_lock) _sessions.Remove(session);
            Interlocked.Increment(ref HostFor(session.Host).ConnectionsClosed);
            Moved();
        }

        /// <summary>
        /// One host as the network sees it. Counters change with Interlocked, the rest under <see cref="Lock"/>.
        /// </summary>
        internal sealed class HostState(string host)
        {
            private static readonly Task<bool> Readable = Task.FromResult(true);

            private TaskCompletionSource<bool>? _unstalled;

            public readonly object Lock = new();

            /// <summary>
            /// The open connections to the host, of either mode.
            /// </summary>
            public readonly List<Session> Sessions = new();

            public long RequestsReceived;
            public long RequestsAnswered;
            public long ConnectionsOpened;
            public long ConnectionsClosed;
            public long ConnectionsRefused;
            public long PartialReadsByServer;
            public long PartialReadsBySdk;
            public long PushesSent;
            public long SelectBucketsFailed;

            public string Host { get; } = host;

            public bool Hung { get; set; }

            public bool Refused { get; set; }

            public bool IsStalled => Volatile.Read(ref _unstalled) is not null;

            public void Stall()
            {
                _unstalled ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public void Unstall()
            {
                var unstalled = _unstalled;
                _unstalled = null;
                unstalled?.TrySetResult(true);
            }

            /// <summary>
            /// Completes once the host reads again.
            /// </summary>
            public Task WhenReadable()
            {
                lock (Lock) return _unstalled?.Task ?? Readable;
            }
        }

        /// <summary>
        /// The node end of one stream connection. A read loop answers each request frame, and a write loop sends the
        /// replies and notifications in order.
        /// </summary>
        internal sealed class StreamSession : Session
        {
            private readonly FakeClusterServer _server;
            private readonly HostState _state;
            private readonly Pipe _toServer;
            private readonly Pipe _toClient;
            private readonly Chunks? _readChunks;
            private readonly Chunks? _writeChunks;
            private readonly Channel<SlicedMemoryOwner<byte>> _outbound =
                Channel.CreateUnbounded<SlicedMemoryOwner<byte>>(new UnboundedChannelOptions { SingleReader = true });

            // Requests to answer and notifications to send once the host stops hanging. Guarded by the host lock.
            private readonly Queue<Held> _held = new();
            private readonly CancellationTokenSource _closing = new();
            private readonly TaskCompletionSource<bool> _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _repliesHeld;
            private int _pushesHeld;
            private int _outboundQueued;
            private long _bytesToServer;
            private long _bytesToClient;
            private int _isClosed;

            public StreamSession(FakeClusterServer server, HostState state, HostEndpointWithPort endPoint, bool isSeed,
                NetworkOptions options, int index) : base(state.Host, endPoint, isSeed)
            {
                if (options.BufferSize < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(options), options.BufferSize, "The buffer needs room for 1 byte.");
                }

                _server = server;
                _state = state;
                var pipeOptions = new PipeOptions(pauseWriterThreshold: options.BufferSize,
                    resumeWriterThreshold: Math.Max(1, options.BufferSize / 2), useSynchronizationContext: false);
                _toServer = new Pipe(pipeOptions);
                _toClient = new Pipe(pipeOptions);

                Chunks? clientReads = null, clientWrites = null;
                if (options.Fragment)
                {
                    var seed = unchecked(options.Seed * 1_000_003 + index * 4);
                    _readChunks = new Chunks(seed);
                    _writeChunks = new Chunks(seed + 1);
                    clientReads = new Chunks(seed + 2);
                    clientWrites = new Chunks(seed + 3);
                }

                Client = new ClientStream(this, state, _toClient.Reader, _toServer.Writer, clientReads, clientWrites,
                    new IPEndPoint(IPAddress.Loopback, 40000 + index % 20000), new IPEndPoint(IPAddress.Loopback, endPoint.Port));
            }

            public ClientStream Client { get; }

            public override bool IsClosed => Volatile.Read(ref _isClosed) == 1;

            public override int RepliesHeld => Volatile.Read(ref _repliesHeld);

            public override int PushesHeld => Volatile.Read(ref _pushesHeld);

            public override int BlockedWrites => Client.BlockedWrites;

            /// <summary>
            /// True while bytes or requests are on their way on this connection.
            /// </summary>
            public bool IsBusy =>
                !IsClosed
                && (Volatile.Read(ref _outboundQueued) > 0
                    || Interlocked.Read(ref _bytesToClient) != Client.BytesRead
                    || (!_state.IsStalled && Client.BytesWritten != Interlocked.Read(ref _bytesToServer)));

            public Task Start() => Task.WhenAll(Task.Run(ReadRequestsAsync), Task.Run(WriteRepliesAsync));

            public string Traffic() =>
                $"{this} sdkWrote={Client.BytesWritten} nodeRead={Interlocked.Read(ref _bytesToServer)} " +
                $"nodeWrote={Interlocked.Read(ref _bytesToClient)} sdkRead={Client.BytesRead} queued={Volatile.Read(ref _outboundQueued)}";

            public void Moved(long amount) => _server.Moved(amount);

            /// <summary>
            /// Makes the read loop look at the stream again, for a frame that just broke.
            /// </summary>
            public void WakeReader() => _toServer.Reader.CancelPendingRead();

            internal override void Push(SlicedMemoryOwner<byte> frame)
            {
                lock (_state.Lock)
                {
                    if (IsClosed)
                    {
                        frame.Dispose();
                        return;
                    }

                    if (_state.Hung)
                    {
                        _held.Enqueue(new Held(null, frame));
                        _pushesHeld++;
                    }
                    else
                    {
                        Send(frame);
                    }
                }
                _server.Moved();
            }

            /// <summary>
            /// Answers the held requests and sends the held notifications. The caller holds the host lock.
            /// </summary>
            public void ReleaseHeld()
            {
                while (_held.Count > 0)
                {
                    var held = _held.Dequeue();
                    if (held.Request is { } request)
                    {
                        _repliesHeld--;
                        Send(_server.Answer(this, request));
                    }
                    else
                    {
                        _pushesHeld--;
                        Send(held.Push);
                    }
                }
                _server.Moved();
            }

            /// <summary>
            /// Ends the connection from the node side. Both loops stop, and the SDK reads the end of the stream.
            /// </summary>
            public void Close()
            {
                lock (_state.Lock)
                {
                    if (Interlocked.Exchange(ref _isClosed, 1) == 1) return;

                    while (_held.Count > 0)
                    {
                        var held = _held.Dequeue();
                        if (held.Request is null) held.Push.Dispose();
                    }
                    _repliesHeld = 0;
                    _pushesHeld = 0;
                    _state.Sessions.Remove(this);
                }

                _outbound.Writer.TryComplete();
                _closing.Cancel();
                _closed.TrySetResult(true);
                _server.OnStreamClosed(this);
            }

            private async Task ReadRequestsAsync()
            {
                var reader = _toServer.Reader;
                var token = _closing.Token;
                // Bytes past the first unread frame that the last pass looked at.
                long examined = 0;
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        var readable = _state.WhenReadable();
                        if (!readable.IsCompleted)
                        {
                            await Task.WhenAny(readable, _closed.Task).ConfigureAwait(false);
                            continue;
                        }

                        var result = await reader.ReadAsync(token).ConfigureAwait(false);
                        var buffer = result.Buffer;
                        if (_state.IsStalled)
                        {
                            // Leave the bytes in the pipe, so the SDK writer fills it and waits.
                            reader.AdvanceTo(buffer.Start);
                            continue;
                        }

                        var visible = _readChunks is null ? buffer.Length : Math.Min(buffer.Length, examined + _readChunks.Next());
                        var window = buffer.Slice(0, visible);
                        var read = Interlocked.Read(ref _bytesToServer);
                        var brokenAt = Client.BrokenAt;
                        var broken = false;
                        while (true)
                        {
                            if (brokenAt >= 0 && read + buffer.Slice(0, window.Start).Length >= brokenAt)
                            {
                                broken = true;
                                break;
                            }
                            if (!TryReadFrame(ref window, out var frame)) break;

                            OnRequest(frame.ToArray());
                        }

                        var consumed = buffer.Slice(0, window.Start).Length;
                        if (consumed > 0)
                        {
                            Interlocked.Add(ref _bytesToServer, consumed);
                            _server.Moved(consumed);
                        }
                        if (!window.IsEmpty)
                        {
                            Interlocked.Increment(ref _state.PartialReadsByServer);
                        }

                        examined = window.Length;
                        reader.AdvanceTo(window.Start, window.End);
                        if (broken || (result.IsCompleted && visible == buffer.Length)) break;
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                }
                finally
                {
                    reader.Complete();
                    Close();
                }
            }

            private void OnRequest(byte[] packet)
            {
                Interlocked.Increment(ref _server._requestsInProcess);
                try
                {
                    _server.Receive(this, packet);
                    lock (_state.Lock)
                    {
                        if (IsClosed) return;

                        if (_state.Hung)
                        {
                            _held.Enqueue(new Held(packet, default));
                            _repliesHeld++;
                        }
                        else
                        {
                            Send(_server.Answer(this, packet));
                        }
                    }
                }
                finally
                {
                    Interlocked.Decrement(ref _server._requestsInProcess);
                    _server.Moved();
                }
            }

            private void Send(SlicedMemoryOwner<byte> frame)
            {
                Interlocked.Increment(ref _outboundQueued);
                if (!_outbound.Writer.TryWrite(frame))
                {
                    Interlocked.Decrement(ref _outboundQueued);
                    frame.Dispose();
                }
            }

            private async Task WriteRepliesAsync()
            {
                var writer = _toClient.Writer;
                var token = _closing.Token;
                try
                {
                    while (await _outbound.Reader.WaitToReadAsync(token).ConfigureAwait(false))
                    {
                        while (_outbound.Reader.TryRead(out var frame))
                        {
                            try
                            {
                                if (!await WriteAsync(writer, frame.Memory, token).ConfigureAwait(false)) return;
                            }
                            finally
                            {
                                frame.Dispose();
                                Interlocked.Decrement(ref _outboundQueued);
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                }
                finally
                {
                    while (_outbound.Reader.TryRead(out var frame))
                    {
                        frame.Dispose();
                        Interlocked.Decrement(ref _outboundQueued);
                    }
                    writer.Complete();
                    Close();
                }
            }

            // False once the SDK end is gone.
            private async ValueTask<bool> WriteAsync(PipeWriter writer, ReadOnlyMemory<byte> data, CancellationToken token)
            {
                while (!data.IsEmpty)
                {
                    var size = _writeChunks is null ? data.Length : Math.Min(data.Length, _writeChunks.Next());
                    writer.Write(data.Span.Slice(0, size));
                    data = data.Slice(size);
                    Interlocked.Add(ref _bytesToClient, size);
                    _server.Moved(size);
                    var flush = await writer.FlushAsync(token).ConfigureAwait(false);
                    if (flush.IsCompleted) return false;
                }
                return true;
            }

            private static bool TryReadFrame(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> frame)
            {
                frame = default;
                if (buffer.Length < OperationHeader.Length) return false;

                Span<byte> bodyLength = stackalloc byte[sizeof(int)];
                buffer.Slice(HeaderOffsets.BodyLength, sizeof(int)).CopyTo(bodyLength);
                var size = OperationHeader.Length + BinaryPrimitives.ReadInt32BigEndian(bodyLength);
                if (buffer.Length < size) return false;

                frame = buffer.Slice(0, size);
                buffer = buffer.Slice(size);
                return true;
            }

            private readonly struct Held(byte[]? request, SlicedMemoryOwner<byte> push)
            {
                public byte[]? Request { get; } = request;

                public SlicedMemoryOwner<byte> Push { get; } = push;
            }
        }

        /// <summary>
        /// The SDK end of one stream connection, for a <see cref="MultiplexingConnection"/>.
        /// </summary>
        /// <remarks>
        /// Disposing it ends the connection for the node too, like closing a socket.
        /// A write that is cancelled after part of it went out leaves a broken frame on the stream. The node answers the
        /// requests before it, then drops the connection once it reads up to it, because the rest would not parse.
        /// </remarks>
        internal sealed class ClientStream : Stream
        {
            private readonly StreamSession _session;
            private readonly HostState _state;
            private readonly PipeReader _input;
            private readonly PipeWriter _output;
            private readonly Chunks? _readChunks;
            private readonly Chunks? _writeChunks;
            private FrameTracker _frames;
            private long _bytesRead;
            private long _bytesWritten;
            private long _brokenAt = -1;
            private int _blockedWrites;
            private int _disposed;

            internal ClientStream(StreamSession session, HostState state, PipeReader input, PipeWriter output,
                Chunks? readChunks, Chunks? writeChunks, IPEndPoint localEndPoint, IPEndPoint remoteEndPoint)
            {
                _session = session;
                _state = state;
                _input = input;
                _output = output;
                _readChunks = readChunks;
                _writeChunks = writeChunks;
                LocalEndPoint = localEndPoint;
                RemoteEndPoint = remoteEndPoint;
            }

            public IPEndPoint LocalEndPoint { get; }

            public IPEndPoint RemoteEndPoint { get; }

            public long BytesRead => Interlocked.Read(ref _bytesRead);

            public long BytesWritten => Interlocked.Read(ref _bytesWritten);

            public int BlockedWrites => Volatile.Read(ref _blockedWrites);

            /// <summary>
            /// Where the first broken frame starts in the bytes to the node, or -1.
            /// </summary>
            public long BrokenAt => Interlocked.Read(ref _brokenAt);

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush()
            {
            }

            public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override int Read(byte[] buffer, int offset, int count) =>
                throw new NotSupportedException("The SDK reads a connection asynchronously.");

            public override void Write(byte[] buffer, int offset, int count) =>
                throw new NotSupportedException("The SDK writes a connection asynchronously.");

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
                ReadCoreAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
                WriteCoreAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

#if NET6_0_OR_GREATER
            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
                ReadCoreAsync(buffer, cancellationToken);

            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
                WriteCoreAsync(buffer, cancellationToken);
#endif

            private async ValueTask<int> ReadCoreAsync(Memory<byte> destination, CancellationToken cancellationToken)
            {
                ThrowIfDisposed();
                while (true)
                {
                    var result = await _input.ReadAsync(cancellationToken).ConfigureAwait(false);
                    var buffer = result.Buffer;
                    if (Volatile.Read(ref _disposed) == 1)
                    {
                        _input.AdvanceTo(buffer.Start);
                        throw new ObjectDisposedException(nameof(ClientStream));
                    }

                    if (destination.IsEmpty || buffer.IsEmpty)
                    {
                        _input.AdvanceTo(buffer.Start);
                        if (result.IsCompleted || destination.IsEmpty) return 0;
                        continue;
                    }

                    var size = (int)Math.Min(buffer.Length, destination.Length);
                    if (_readChunks is not null) size = Math.Min(size, _readChunks.Next());
                    var data = buffer.Slice(0, size);
                    data.CopyTo(destination.Span);
                    _input.AdvanceTo(data.End);

                    if (_frames.Read(destination.Span.Slice(0, size)))
                    {
                        Interlocked.Increment(ref _state.PartialReadsBySdk);
                    }
                    Interlocked.Add(ref _bytesRead, size);
                    _session.Moved(size);
                    return size;
                }
            }

            private async ValueTask WriteCoreAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
            {
                ThrowIfDisposed();
                cancellationToken.ThrowIfCancellationRequested();
                // The SDK writes one request per call.
                var frameStart = Interlocked.Read(ref _bytesWritten);
                using var registration = cancellationToken.CanBeCanceled
                    ? cancellationToken.Register(static output => ((PipeWriter)output!).CancelPendingFlush(), _output)
                    : default;

                while (!data.IsEmpty)
                {
                    var size = _writeChunks is null ? data.Length : Math.Min(data.Length, _writeChunks.Next());
                    _output.Write(data.Span.Slice(0, size));
                    data = data.Slice(size);
                    Interlocked.Add(ref _bytesWritten, size);
                    _session.Moved(size);

                    FlushResult flush;
                    do
                    {
                        // The registration above cancels the flush, so the data is in the pipe whether it waits or not.
                        var pending = _output.FlushAsync(CancellationToken.None);
                        if (pending.IsCompleted)
                        {
                            flush = pending.GetAwaiter().GetResult();
                        }
                        else
                        {
                            Interlocked.Increment(ref _blockedWrites);
                            try
                            {
                                flush = await pending.ConfigureAwait(false);
                            }
                            finally
                            {
                                Interlocked.Decrement(ref _blockedWrites);
                            }
                        }
                    }
                    // A cancel that came after the last flush of an earlier write cancels the next flush once.
                    while (flush.IsCanceled && Volatile.Read(ref _disposed) == 0 && !cancellationToken.IsCancellationRequested);

                    if (Volatile.Read(ref _disposed) == 1)
                    {
                        throw new ObjectDisposedException(nameof(ClientStream));
                    }
                    if (flush.IsCompleted)
                    {
                        throw new IOException($"The node {_session.Host} closed the connection.");
                    }
                    if (flush.IsCanceled)
                    {
                        if (!data.IsEmpty)
                        {
                            Interlocked.CompareExchange(ref _brokenAt, frameStart, -1);
                            _session.WakeReader();
                        }
                        throw new OperationCanceledException(cancellationToken);
                    }
                }
            }

            private void ThrowIfDisposed()
            {
                if (Volatile.Read(ref _disposed) == 1)
                {
                    throw new ObjectDisposedException(nameof(ClientStream));
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 1) return;

                // Wakes a read or write that waits. The node end then completes both pipes.
                _input.CancelPendingRead();
                _output.CancelPendingFlush();
                _session.Close();
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Random chunk sizes. One in four is shorter than a header, so headers split too.
        /// </summary>
        internal sealed class Chunks(int seed)
        {
            private readonly Random _random = new(seed);

            public int Next() => _random.Next(4) == 0 ? _random.Next(1, OperationHeader.Length) : _random.Next(1, 1500);
        }

        /// <summary>
        /// Follows the frame boundaries of a byte stream, so a read can tell whether it ended inside a frame.
        /// </summary>
        private struct FrameTracker
        {
            private int _headerBytes;
            private long _bodyLength;
            private long _bodyLeft;

            /// <returns>True when the bytes end inside a frame.</returns>
            public bool Read(ReadOnlySpan<byte> data)
            {
                var i = 0;
                while (i < data.Length)
                {
                    if (_headerBytes < OperationHeader.Length)
                    {
                        var take = Math.Min(OperationHeader.Length - _headerBytes, data.Length - i);
                        for (var k = 0; k < take; k++)
                        {
                            var offset = _headerBytes + k;
                            if (offset >= HeaderOffsets.BodyLength && offset < HeaderOffsets.BodyLength + sizeof(int))
                            {
                                _bodyLength = (_bodyLength << 8) | data[i + k];
                            }
                        }
                        _headerBytes += take;
                        i += take;
                        if (_headerBytes == OperationHeader.Length)
                        {
                            _bodyLeft = _bodyLength;
                            _bodyLength = 0;
                            if (_bodyLeft == 0) _headerBytes = 0;
                        }
                    }
                    else
                    {
                        var take = (int)Math.Min(_bodyLeft, data.Length - i);
                        _bodyLeft -= take;
                        i += take;
                        if (_bodyLeft == 0) _headerBytes = 0;
                    }
                }
                return _headerBytes != 0;
            }
        }
    }
}

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2026 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
