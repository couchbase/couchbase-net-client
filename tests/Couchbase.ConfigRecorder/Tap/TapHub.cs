using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Couchbase.Core.IO.Operations;
using Snappier;

namespace Couchbase.ConfigRecorder.Tap;

/// <summary>
/// Decodes the frames that the connection taps copied and writes them as events.
/// One task handles all connections in arrival order, so a request is always seen before its response.
/// </summary>
internal sealed class TapHub : IAsyncDisposable
{
    public const string SdkRole = "sdk";
    public const string ProbeRole = "probe";

    private readonly Recorder _recorder;
    private readonly Channel<TapItem> _channel = Channel.CreateUnbounded<TapItem>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Task _processor;
    private long _sdkNotMyVBucket;
    private long _sdkNotMyVBucketTotal;
    private int _sdkConnections;
    private int _probeConnections;

    public TapHub(Recorder recorder)
    {
        _recorder = recorder;
        _processor = Task.Run(ProcessLoopAsync);
    }

    /// <summary>
    /// Raised on the hub task for each clustermap push on a probe connection, with host, port and
    /// whether the push names a bucket.
    /// </summary>
    public event Action<string, int, bool>? ProbePushed;

    public int SdkConnections => Volatile.Read(ref _sdkConnections);

    public int ProbeConnections => Volatile.Read(ref _probeConnections);

    public long SdkNotMyVBucketTotal => Interlocked.Read(ref _sdkNotMyVBucketTotal);

    /// <summary>
    /// Returns the not-my-vbucket responses seen on SDK connections since the last call.
    /// </summary>
    public long TakeSdkNotMyVBucket() => Interlocked.Exchange(ref _sdkNotMyVBucket, 0);

    public ConnectionTap CreateTap(string role, string host, int port, bool tls, string remote, string local) =>
        new(this, role, host, port, tls, remote, local);

    internal void Post(TapItem item) => _channel.Writer.TryWrite(item);

    internal void CountNotMyVBucket(bool sdk)
    {
        if (!sdk) return;
        Interlocked.Increment(ref _sdkNotMyVBucket);
        Interlocked.Increment(ref _sdkNotMyVBucketTotal);
    }

    private async Task ProcessLoopAsync()
    {
        await foreach (var item in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                Process(item);
            }
            catch (Exception e)
            {
                var fields = Tags(item.Tap);
                fields["error"] = e.GetType().Name;
                fields["detail"] = e.Message;
                Write(item, "frame-error", fields);
            }
        }
    }

    private void Process(TapItem item)
    {
        var tap = item.Tap;
        switch (item.Kind)
        {
            case TapItemKind.Connect:
                if (tap.IsSdk) Interlocked.Increment(ref _sdkConnections);
                else Interlocked.Increment(ref _probeConnections);
                var connect = Tags(tap);
                connect["remote"] = tap.Remote;
                connect["local"] = tap.Local;
                connect["tls"] = tap.Tls;
                Write(item, "connect", connect);
                break;
            case TapItemKind.ReadEnd:
                tap.EndReason ??= item.Detail;
                break;
            case TapItemKind.Stopped:
                var stopped = Tags(tap);
                stopped["detail"] = item.Detail;
                Write(item, "tap-stopped", stopped);
                _recorder.Error("tap", $"Stopped tapping connection {tap.ConnectionId} to {tap.Host}:{tap.Port}. {item.Detail}");
                break;
            case TapItemKind.Close:
                // A connection that failed its TLS handshake was never attached.
                if (tap.ConnectionId.Length == 0) return;
                if (tap.IsSdk) Interlocked.Decrement(ref _sdkConnections);
                else Interlocked.Decrement(ref _probeConnections);

                var close = Tags(tap);
                close["reason"] = tap.EndReason ?? "closed by client";
                if (tap.SuppressedReplies > 0)
                {
                    close["suppressedReplies"] = tap.SuppressedReplies;
                }

                Write(item, "close", close);
                break;
            case TapItemKind.Frame:
                if (item.FromServer)
                {
                    ProcessFromServer(item);
                }
                else
                {
                    ProcessToServer(item);
                }

                break;
        }
    }

    private static void ProcessToServer(TapItem item)
    {
        var frame = new Frame(item.Header!, item.Body!);
        var tap = item.Tap;
        switch (frame.Opcode)
        {
            case Protocol.OpGetClusterConfig:
                ulong? epoch = null, revision = null;
                if (frame.Extras.Length >= 16)
                {
                    epoch = BinaryPrimitives.ReadUInt64BigEndian(frame.Extras);
                    revision = BinaryPrimitives.ReadUInt64BigEndian(frame.Extras[8..]);
                }

                tap.Pending[frame.Opaque] = new PendingRequest(frame.Opcode, item.Timestamp, epoch, revision);
                break;
            case Protocol.OpSelectBucket:
                tap.Pending[frame.Opaque] = new PendingRequest(frame.Opcode, item.Timestamp, Key: Encoding.UTF8.GetString(frame.Key));
                break;
            case Protocol.OpHello:
                tap.Pending[frame.Opaque] = new PendingRequest(frame.Opcode, item.Timestamp, Features: ReadFeatures(frame.Value));
                break;
        }
    }

    private void ProcessFromServer(TapItem item)
    {
        var frame = new Frame(item.Header!, item.Body!);
        var tap = item.Tap;

        if (frame.Magic == Protocol.MagicServerRequest)
        {
            ProcessPush(item, frame);
            return;
        }

        tap.Pending.Remove(frame.Opaque, out var pending);
        if (pending is not null && pending.Opcode != frame.Opcode)
        {
            pending = null;
        }

        switch (frame.Opcode)
        {
            case Protocol.OpGetClusterConfig:
                ProcessConfigReply(item, frame, pending);
                return;
            case Protocol.OpSelectBucket:
                if (frame.Status == Protocol.StatusSuccess && pending?.Key is not null)
                {
                    tap.Bucket = pending.Key;
                }

                var select = Tags(tap);
                select["selected"] = pending?.Key;
                select["status"] = StatusName(frame.Status);
                Write(item, "select-bucket", select);
                return;
            case Protocol.OpHello:
                var hello = Tags(tap);
                hello["status"] = StatusName(frame.Status);
                hello["granted"] = ToArray(ReadFeatures(frame.Value));
                if (pending?.Features is not null)
                {
                    hello["requested"] = ToArray(pending.Features);
                }

                Write(item, "hello", hello);
                return;
        }

        if (frame.Status == Protocol.StatusNotMyVBucket && frame.Value.Length > 0)
        {
            var nmvb = Tags(tap);
            nmvb["opcode"] = ((OpCode)frame.Opcode).ToString();
            nmvb["opaque"] = frame.Opaque;
            AddBody(nmvb, frame);
            Write(item, "nmvb", nmvb);
        }
    }

    private void ProcessConfigReply(TapItem item, Frame frame, PendingRequest? pending)
    {
        var tap = item.Tap;
        string? hash = null;
        if (!tap.IsSdk && frame.Status == Protocol.StatusSuccess)
        {
            // The probe asks often. Keep only replies that carry a different body.
            if (frame.Value.Length == 0)
            {
                tap.SuppressedReplies++;
                return;
            }

            hash = _recorder.StoreConfig(Decompress(frame));
            if (hash == tap.LastConfigHash)
            {
                tap.SuppressedReplies++;
                return;
            }

            tap.LastConfigHash = hash;
        }

        var fields = Tags(tap);
        fields["status"] = StatusName(frame.Status);
        fields["opaque"] = frame.Opaque;
        if (pending is not null)
        {
            fields["rttMs"] = Math.Round((item.Timestamp - pending.Timestamp) * 1000.0 / System.Diagnostics.Stopwatch.Frequency, 3);
            if (pending.Epoch.HasValue)
            {
                fields["knownEpoch"] = pending.Epoch.Value;
                fields["knownRev"] = pending.Revision!.Value;
            }
        }

        AddBody(fields, frame, hash);
        Write(item, "config", fields);
    }

    private void ProcessPush(TapItem item, Frame frame)
    {
        if (frame.Opcode != Protocol.OpClusterMapChangeNotification) return;

        var fields = Tags(item.Tap);
        fields["pushBucket"] = Encoding.UTF8.GetString(frame.Key);
        if (frame.Extras.Length >= 16)
        {
            fields["epoch"] = BinaryPrimitives.ReadUInt64BigEndian(frame.Extras);
            fields["rev"] = BinaryPrimitives.ReadUInt64BigEndian(frame.Extras[8..]);
        }
        else if (frame.Extras.Length >= 4)
        {
            fields["rev"] = BinaryPrimitives.ReadUInt32BigEndian(frame.Extras);
        }

        fields["brief"] = frame.Value.Length == 0;
        if (frame.Value.Length > 0)
        {
            AddBody(fields, frame);
        }

        Write(item, "push", fields);

        if (!item.Tap.IsSdk)
        {
            try
            {
                ProbePushed?.Invoke(item.Tap.Host, item.Tap.Port, frame.Key.Length > 0);
            }
            catch (Exception e)
            {
                _recorder.Error("probe", "Could not wake the probe after a push.", e);
            }
        }
    }

    private void AddBody(JsonObject fields, Frame frame, string? knownHash = null)
    {
        fields["datatype"] = frame.Datatype;
        fields["length"] = frame.Value.Length;
        if (frame.Value.Length == 0) return;

        var body = Decompress(frame);
        fields["hash"] = knownHash ?? _recorder.StoreConfig(body);
        var version = ConfigJson.ReadVersion(body);
        if (version.Error is not null)
        {
            fields["parseError"] = version.Error;
            return;
        }

        if (version.Rev.HasValue) fields["bodyRev"] = version.Rev.Value;
        if (version.RevEpoch.HasValue) fields["bodyRevEpoch"] = version.RevEpoch.Value;
        fields["name"] = version.Name;
    }

    private static byte[] Decompress(Frame frame) =>
        (frame.Datatype & Protocol.DatatypeSnappy) != 0
            ? Snappy.DecompressToArray(frame.Value)
            : frame.Value.ToArray();

    private static string[] ReadFeatures(ReadOnlySpan<byte> value)
    {
        var features = new string[value.Length / 2];
        for (var i = 0; i < features.Length; i++)
        {
            var code = BinaryPrimitives.ReadUInt16BigEndian(value[(i * 2)..]);
            features[i] = Enum.IsDefined(typeof(ServerFeatures), (short)code)
                ? ((ServerFeatures)code).ToString()
                : $"0x{code:x2}";
        }

        return features;
    }

    private static string StatusName(ushort status) =>
        Enum.IsDefined(typeof(ResponseStatus), (int)status)
            ? ((ResponseStatus)status).ToString()
            : $"0x{status:x4}";

    private static JsonArray ToArray(string[] values) => new(values.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray());

    private static JsonObject Tags(ConnectionTap tap) => new()
    {
        ["host"] = tap.Host,
        ["port"] = tap.Port,
        ["cid"] = tap.ConnectionId,
        ["bucket"] = tap.Bucket,
    };

    private void Write(TapItem item, string kind, JsonObject fields)
    {
        var stream = item.Tap.IsSdk ? "tap" : "probe-tap";
        if (item.Timestamp != 0)
        {
            _recorder.Write(stream, kind, fields, item.Timestamp, item.WallClock);
        }
        else
        {
            _recorder.Write(stream, kind, fields);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        await _processor.ConfigureAwait(false);
    }

    /// <summary>
    /// The parts of one frame. Alternative magics carry flexible framing extras before the extras.
    /// </summary>
    private readonly ref struct Frame
    {
        public Frame(byte[] header, byte[] body)
        {
            Magic = header[0];
            Opcode = header[1];
            int framingLength;
            int keyLength;
            if (Magic is Protocol.MagicAltRequest or Protocol.MagicAltResponse)
            {
                framingLength = header[2];
                keyLength = header[3];
            }
            else
            {
                framingLength = 0;
                keyLength = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2));
            }

            var extrasLength = header[4];
            Datatype = header[5];
            Status = Magic is Protocol.MagicResponse or Protocol.MagicAltResponse
                ? BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(6))
                : (ushort)0;
            // The SDK writes the opaque little endian, so this matches the opaque in sdk.log.
            Opaque = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12));

            var valueOffset = framingLength + extrasLength + keyLength;
            if (valueOffset > body.Length)
            {
                throw new InvalidDataException($"Frame sections ({valueOffset} bytes) exceed the body ({body.Length} bytes).");
            }

            var span = body.AsSpan();
            Extras = span.Slice(framingLength, extrasLength);
            Key = span.Slice(framingLength + extrasLength, keyLength);
            Value = span[valueOffset..];
        }

        public byte Magic { get; }
        public byte Opcode { get; }
        public byte Datatype { get; }
        public ushort Status { get; }
        public uint Opaque { get; }
        public ReadOnlySpan<byte> Extras { get; }
        public ReadOnlySpan<byte> Key { get; }
        public ReadOnlySpan<byte> Value { get; }
    }
}
