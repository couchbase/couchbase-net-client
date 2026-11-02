using System.Buffers.Binary;
using System.Diagnostics;

namespace Couchbase.ConfigRecorder.Tap;

/// <summary>
/// Watches the bytes of one KV connection. The read and write paths only cut frames and copy the
/// few frames of interest. <see cref="TapHub"/> decodes them on its own task.
/// </summary>
internal sealed class ConnectionTap
{
    private readonly TapHub _hub;
    private readonly FrameScanner _fromServer;
    private readonly FrameScanner _toServer;
    private volatile bool _stopped;
    private int _closed;

    public ConnectionTap(TapHub hub, string role, string host, int port, bool tls, string remote, string local)
    {
        _hub = hub;
        Role = role;
        Host = host;
        Port = port;
        Tls = tls;
        Remote = remote;
        Local = local;
        _fromServer = new FrameScanner(this, fromServer: true);
        _toServer = new FrameScanner(this, fromServer: false);
    }

    public string Role { get; }
    public string Host { get; }
    public int Port { get; }
    public bool Tls { get; }
    public string Remote { get; }
    public string Local { get; }
    public string ConnectionId { get; private set; } = "";
    public bool IsSdk => Role == TapHub.SdkRole;

    // Only read and written by the TapHub task.
    internal Dictionary<uint, PendingRequest> Pending { get; } = new();
    internal string? Bucket { get; set; }
    internal string? LastConfigHash { get; set; }
    internal long SuppressedReplies { get; set; }
    internal string? EndReason { get; set; }

    public void Attach(ulong connectionId)
    {
        ConnectionId = connectionId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _hub.Post(new TapItem(TapItemKind.Connect, this));
    }

    public void OnRead(ReadOnlySpan<byte> data)
    {
        if (_stopped || data.IsEmpty) return;
        try
        {
            _fromServer.Feed(data);
        }
        catch (Exception e)
        {
            Stop("read", e);
        }
    }

    public void OnWrite(ReadOnlySpan<byte> data)
    {
        if (_stopped || data.IsEmpty) return;
        try
        {
            _toServer.Feed(data);
        }
        catch (Exception e)
        {
            Stop("write", e);
        }
    }

    public void OnReadEnd(Exception? exception)
    {
        try
        {
            _hub.Post(new TapItem(TapItemKind.ReadEnd, this, Detail: exception is null ? "eof" : $"{exception.GetType().Name}, {exception.Message}"));
        }
        catch
        {
            // Never throw into the SDK read path.
        }
    }

    public void OnClose()
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0) return;
        try
        {
            _hub.Post(new TapItem(TapItemKind.Close, this));
        }
        catch
        {
            // Never throw into the SDK.
        }
    }

    /// <summary>
    /// Stops tapping this connection. The SDK traffic continues untouched.
    /// </summary>
    public void Stop(string direction, Exception exception)
    {
        if (_stopped) return;
        _stopped = true;
        try
        {
            _hub.Post(new TapItem(TapItemKind.Stopped, this, Detail: $"{direction} side, {exception.GetType().Name}, {exception.Message}"));
        }
        catch
        {
            // Never throw into the SDK.
        }
    }

    internal void Emit(bool fromServer, byte[] header, byte[] body) =>
        _hub.Post(new TapItem(TapItemKind.Frame, this, fromServer, header, body, Stopwatch.GetTimestamp(), DateTime.UtcNow));

    internal void CountNotMyVBucket() => _hub.CountNotMyVBucket(IsSdk);
}

internal enum TapItemKind
{
    Connect,
    Frame,
    ReadEnd,
    Stopped,
    Close,
}

internal sealed record TapItem(
    TapItemKind Kind,
    ConnectionTap Tap,
    bool FromServer = false,
    byte[]? Header = null,
    byte[]? Body = null,
    long Timestamp = 0,
    DateTime WallClock = default,
    string? Detail = null);

internal sealed record PendingRequest(byte Opcode, long Timestamp, ulong? Epoch = null, ulong? Revision = null, string? Key = null, string[]? Features = null);

/// <summary>
/// Cuts a byte stream into memcached binary frames, one header at a time. A read can hold part
/// of a frame or several frames. Frames of no interest are skipped without a copy.
/// </summary>
internal sealed class FrameScanner(ConnectionTap tap, bool fromServer)
{
    private const int HeaderLength = 24;
    private const uint MaxBodyLength = 64 * 1024 * 1024;

    private readonly byte[] _header = new byte[HeaderLength];
    private int _headerFill;
    private long _skip;
    private byte[]? _pendingHeader;
    private byte[]? _body;
    private int _bodyFill;

    public void Feed(ReadOnlySpan<byte> data)
    {
        while (!data.IsEmpty)
        {
            if (_skip > 0)
            {
                var n = (int)Math.Min(_skip, data.Length);
                _skip -= n;
                data = data[n..];
                continue;
            }

            if (_body is not null)
            {
                var n = Math.Min(_body.Length - _bodyFill, data.Length);
                data[..n].CopyTo(_body.AsSpan(_bodyFill));
                _bodyFill += n;
                data = data[n..];
                if (_bodyFill == _body.Length)
                {
                    tap.Emit(fromServer, _pendingHeader!, _body);
                    _body = null;
                    _pendingHeader = null;
                }

                continue;
            }

            var h = Math.Min(HeaderLength - _headerFill, data.Length);
            data[..h].CopyTo(_header.AsSpan(_headerFill));
            _headerFill += h;
            data = data[h..];
            if (_headerFill < HeaderLength) return;

            _headerFill = 0;
            OnHeader();
        }
    }

    private void OnHeader()
    {
        var magic = _header[0];
        var opcode = _header[1];
        var bodyLength = BinaryPrimitives.ReadUInt32BigEndian(_header.AsSpan(8));
        if (bodyLength > MaxBodyLength)
        {
            throw new InvalidDataException($"Frame body of {bodyLength} bytes is too large.");
        }

        bool keep;
        if (fromServer)
        {
            switch (magic)
            {
                case Protocol.MagicResponse:
                case Protocol.MagicAltResponse:
                    var status = BinaryPrimitives.ReadUInt16BigEndian(_header.AsSpan(6));
                    if (status == Protocol.StatusNotMyVBucket)
                    {
                        tap.CountNotMyVBucket();
                    }

                    keep = opcode is Protocol.OpGetClusterConfig or Protocol.OpHello or Protocol.OpSelectBucket
                           || (status == Protocol.StatusNotMyVBucket && bodyLength > 0);
                    break;
                case Protocol.MagicServerRequest:
                    keep = opcode == Protocol.OpClusterMapChangeNotification;
                    break;
                default:
                    throw new InvalidDataException($"Unexpected magic 0x{magic:x2} from the server.");
            }
        }
        else
        {
            switch (magic)
            {
                case Protocol.MagicRequest:
                case Protocol.MagicAltRequest:
                    keep = opcode is Protocol.OpGetClusterConfig or Protocol.OpHello or Protocol.OpSelectBucket;
                    break;
                case Protocol.MagicServerResponse:
                    keep = false;
                    break;
                default:
                    throw new InvalidDataException($"Unexpected magic 0x{magic:x2} to the server.");
            }
        }

        if (!keep)
        {
            _skip = bodyLength;
            return;
        }

        var header = _header.ToArray();
        if (bodyLength == 0)
        {
            tap.Emit(fromServer, header, []);
            return;
        }

        _pendingHeader = header;
        _body = new byte[bodyLength];
        _bodyFill = 0;
    }
}

/// <summary>
/// Memcached binary protocol values, as used by the SDK in Magic.cs, OpCode.cs and HeaderOffsets.cs.
/// </summary>
internal static class Protocol
{
    public const byte MagicRequest = 0x80;
    public const byte MagicAltRequest = 0x08;
    public const byte MagicResponse = 0x81;
    public const byte MagicAltResponse = 0x18;
    public const byte MagicServerRequest = 0x82;
    public const byte MagicServerResponse = 0x83;

    public const byte OpGetClusterConfig = 0xb5;
    public const byte OpHello = 0x1f;
    public const byte OpSelectBucket = 0x89;

    // A server request with this opcode is a clustermap change notification.
    public const byte OpClusterMapChangeNotification = 0x01;

    public const ushort StatusSuccess = 0x0000;
    public const ushort StatusNotMyVBucket = 0x0007;

    public const byte DatatypeSnappy = 0x02;
}
