#nullable enable
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Diagnostics;
using Couchbase.Utils;

namespace Couchbase.Test.Common.Configuration
{
    /// <summary>
    /// Plays the data service of a cluster for <see cref="ConfigHarness"/>, with no network.
    /// Each node answers connection setup, serves the cluster map set for it and pushes cluster map changes.
    /// </summary>
    /// <remarks>
    /// A host is a node name, the hostname of the node in nodesExt. Every method that takes a host takes that name.
    /// The server finds the node of a connection from the endpoint the SDK connects to. Read <see cref="NodeAt"/>.
    /// HELLO grants every feature the SDK asks for. A connection serves the map of the bucket it selected,
    /// or the global map when it selected none.
    /// </remarks>
    internal sealed partial class FakeClusterServer
    {
        private static readonly byte[] ErrorMap = Encoding.UTF8.GetBytes("""{"version":2,"revision":1,"errors":{}}""");
        private static readonly byte[] SaslMechanisms = Encoding.UTF8.GetBytes("SCRAM-SHA512 SCRAM-SHA256");
        private static readonly byte[] Manifest = Encoding.UTF8.GetBytes(
            """{"uid":"0","scopes":[{"name":"_default","uid":"0","collections":[{"name":"_default","uid":"0"}]}]}""");

        private readonly object _lock = new();
        private readonly Dictionary<string, BucketMaps> _maps = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _answersLeft = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Queue<byte[]>> _notMyVBucket = new(StringComparer.Ordinal);
        private readonly Dictionary<string, HostState> _hosts = new(StringComparer.Ordinal);
        private readonly List<ConfigFetch> _fetches = new();
        private readonly List<ServerRequest> _received = new();
        private readonly List<Session> _sessions = new();

        // The node name at each alternate address endpoint, from every map set so far. So a node keeps its endpoint
        // after it leaves the maps, and the last map to name an endpoint wins.
        private readonly Dictionary<(string Host, int Port), string> _plainEndpoints = new();
        private readonly Dictionary<(string Host, int Port), string> _tlsEndpoints = new();
        private readonly HashSet<string> _alternateHosts = new(StringComparer.Ordinal);
        private readonly HashSet<(string Map, ConfigVersion Version)> _indexed = new();
        private long _requests;

        /// <summary>
        /// A server that answers inside each send, for <see cref="ConnectionMode.Fake"/>.
        /// </summary>
        public FakeClusterServer()
        {
        }

        /// <summary>
        /// A server that the SDK reaches over in-memory streams, for <see cref="ConnectionMode.Real"/>.
        /// </summary>
        public FakeClusterServer(NetworkOptions network)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
        }

        /// <summary>
        /// True when the SDK connects to the TLS ports. An alternate address endpoint then matches a node by its kvSSL
        /// port, else by its kv port.
        /// </summary>
        public bool Tls { get; set; }

        /// <summary>
        /// The node that takes each connection to an endpoint that matches no node, like the Private Link seed on 11207.
        /// A real endpoint load balancer picks a node that the client cannot see. When null, it is the first node by name
        /// of the latest cluster map the server serves.
        /// </summary>
        public string? SeedNode { get; set; }

        /// <summary>
        /// How many requests the nodes answered, of any kind.
        /// </summary>
        public long RequestCount => Interlocked.Read(ref _requests);

        /// <summary>
        /// The name of the node at the endpoint, or null when the endpoint matches no node, like the Private Link seed.
        /// </summary>
        /// <remarks>
        /// An endpoint on an alternate address host matches the node with that host and port in a map the server was
        /// given. The port is the kv port, or the kvSSL port with <see cref="Tls"/>. Any other host is a node name, so on
        /// the default network the endpoint host is the name.
        /// </remarks>
        public string? NodeAt(HostEndpointWithPort endPoint)
        {
            lock (_lock)
            {
                var endpoints = Tls ? _tlsEndpoints : _plainEndpoints;
                if (endpoints.TryGetValue((endPoint.Host, endPoint.Port), out var name)) return name;
                return _alternateHosts.Contains(endPoint.Host) ? null : endPoint.Host;
            }
        }

        /// <summary>
        /// The node that takes a connection to the endpoint. It is <see cref="SeedNode"/> for an endpoint that matches no node.
        /// </summary>
        /// <exception cref="InvalidOperationException">The endpoint matches no node and the server serves no cluster map.</exception>
        internal (string Host, bool Seed) Route(HostEndpointWithPort endPoint)
        {
            if (NodeAt(endPoint) is { } name) return (name, false);

            lock (_lock)
            {
                return (SeedNode ?? FirstNodeOfLatestClusterMap()
                    ?? throw new InvalidOperationException($"{endPoint} matches no node, and no node serves a cluster map to pick a seed node from."), true);
            }
        }

        private string? FirstNodeOfLatestClusterMap()
        {
            if (!_maps.TryGetValue(BucketConfig.GlobalBucketName, out var maps)) return null;

            var latest = maps.Nodes.Values.Select(n => n.Map).Prepend(maps.Default)
                .Where(m => m is not null)
                .OrderByDescending(m => m!.Version)
                .FirstOrDefault();
            return latest?.Nodes.Select(n => n.Name).OrderBy(n => n, StringComparer.Ordinal).FirstOrDefault();
        }

        /// <summary>
        /// Every request the nodes received, in order. In real mode a request counts once the node has read all of it.
        /// </summary>
        public IReadOnlyList<ServerRequest> Received
        {
            get
            {
                lock (_lock) return _received.ToList();
            }
        }

        public int ReceivedCount(string host, OpCode opCode)
        {
            lock (_lock) return _received.Count(r => r.Host == host && r.OpCode == opCode);
        }

        /// <summary>
        /// What happened on the connections to the host so far.
        /// </summary>
        public HostCounters Counters(string host)
        {
            var state = HostFor(host);
            lock (state.Lock)
            {
                return new HostCounters(state, state.Sessions.Where(s => !s.IsClosed).ToList());
            }
        }

        /// <summary>
        /// Every config fetch the nodes answered, in order.
        /// </summary>
        public IReadOnlyList<ConfigFetch> Fetches
        {
            get
            {
                lock (_lock) return _fetches.ToList();
            }
        }

        public int FetchCount(string host)
        {
            lock (_lock) return _fetches.Count(f => f.Host == host);
        }

        /// <summary>
        /// Forgets the config fetches and the received requests recorded so far. The counters keep counting.
        /// </summary>
        public void ClearFetches()
        {
            lock (_lock)
            {
                _fetches.Clear();
                _received.Clear();
            }
        }

        /// <summary>
        /// Every node serves the map. This drops what the other SetMap calls set for maps of that name.
        /// </summary>
        public void SetMap(ClusterMapBuilder map) => SetMap(Prepare(map));

        /// <summary>
        /// Serializes the map ahead of time, so <see cref="SetMap(ServedMap)"/> does no serialization. Benchmarks use it.
        /// </summary>
        public static ServedMap Prepare(ClusterMapBuilder map) => ServedMap.From(map);

        /// <summary>
        /// Every node serves the prepared map, like <see cref="SetMap(ClusterMapBuilder)"/>.
        /// </summary>
        public void SetMap(ServedMap map)
        {
            lock (_lock)
            {
                var maps = MapsFor(map.Name);
                maps.Default = map;
                maps.Nodes.Clear();
                Index(map);
            }
        }

        /// <summary>
        /// Only these nodes serve the map, so the other nodes lag.
        /// </summary>
        public void SetMap(ClusterMapBuilder map, params string[] hosts) => SetMap(map.Name, ServedMap.From(map), hosts);

        /// <summary>
        /// These nodes serve their current map for <paramref name="fetches"/> more fetches, then this map.
        /// </summary>
        public void SetMapAfter(int fetches, ClusterMapBuilder map, params string[] hosts)
        {
            var served = ServedMap.From(map);
            lock (_lock)
            {
                var maps = MapsFor(map.Name);
                Index(served);
                foreach (var host in hosts)
                {
                    if (!maps.Nodes.TryGetValue(host, out var node))
                    {
                        node = new NodeMap(maps.Default ?? throw new InvalidOperationException($"No node serves {map.Name} yet."));
                        maps.Nodes[host] = node;
                    }
                    node.Next = served;
                    node.FetchesBeforeNext = fetches;
                }
            }
        }

        /// <summary>
        /// These nodes answer <paramref name="fetches"/> more config fetches, then fail every one with a temporary failure.
        /// </summary>
        public void FailFetchesAfter(int fetches, params string[] hosts)
        {
            lock (_lock)
            {
                foreach (var host in hosts)
                {
                    _answersLeft[host] = fetches;
                }
            }
        }

        /// <summary>
        /// These nodes answer every config fetch again, after <see cref="FailFetchesAfter"/>.
        /// </summary>
        public void AnswerFetches(params string[] hosts)
        {
            lock (_lock)
            {
                foreach (var host in hosts)
                {
                    _answersLeft.Remove(host);
                }
            }
        }

        /// <summary>
        /// The next data operation on the node gets not-my-vbucket, with the map as the body.
        /// </summary>
        public void NotMyVBucket(string host, ClusterMapBuilder map)
        {
            lock (_lock)
            {
                if (!_notMyVBucket.TryGetValue(host, out var bodies))
                {
                    bodies = new Queue<byte[]>();
                    _notMyVBucket[host] = bodies;
                }
                bodies.Enqueue(map.ToUtf8Bytes());
            }
        }

        /// <summary>
        /// The node serves the map from now on and pushes it in full to its connections for that map.
        /// </summary>
        public void PushFull(string host, ClusterMapBuilder map)
        {
            var served = ServedMap.From(map);
            SetMap(map.Name, served, [host]);
            var bucket = map.Name == BucketConfig.GlobalBucketName ? null : map.Name;
            foreach (var connection in ConnectionsOn(host))
            {
                if (connection.Bucket != bucket || !connection.Has(ServerFeatures.ClustermapChangeNotification)) continue;

                Notify(connection, Notification(served.Version, bucket, DataType.Json, served.Json));
            }
        }

        /// <summary>
        /// The node pushes only the version, to each connection that negotiated brief notifications.
        /// It does not change the map the node serves.
        /// </summary>
        public void PushBrief(string host, ulong epoch, ulong rev)
        {
            foreach (var connection in ConnectionsOn(host))
            {
                if (!connection.Has(ServerFeatures.ClustermapChangeNotificationBrief)) continue;

                Notify(connection, Notification(new ConfigVersion(epoch, rev), connection.Bucket, DataType.None, []));
            }
        }

        /// <summary>
        /// The node serves the map from now on, then tells its connections that use the map. A connection uses a bucket
        /// map when it selected that bucket, and the global map when it selected none. It gets a brief notification when it
        /// negotiated brief ones, else a full one when it negotiated full ones, else none.
        /// </summary>
        public void Serve(string host, ClusterMapBuilder map) => Serve(host, ServedMap.From(map));

        /// <summary>
        /// Serves a prepared map, like <see cref="Serve(string, ClusterMapBuilder)"/>.
        /// </summary>
        /// <param name="notify">False to only set the map, so the connections learn of it on their next fetch.</param>
        public void Serve(string host, ServedMap map, bool notify = true)
        {
            SetMap(map.Name, map, [host]);
            if (!notify) return;

            var bucket = map.Name == BucketConfig.GlobalBucketName ? null : map.Name;
            foreach (var connection in ConnectionsOn(host))
            {
                if (connection.Bucket != bucket) continue;

                if (connection.Has(ServerFeatures.ClustermapChangeNotificationBrief))
                {
                    Notify(connection, Notification(map.Version, bucket, DataType.None, []));
                }
                else if (connection.Has(ServerFeatures.ClustermapChangeNotification))
                {
                    Notify(connection, Notification(map.Version, bucket, DataType.Json, map.Json));
                }
            }
        }

        private void Notify(Session connection, SlicedMemoryOwner<byte> frame)
        {
            Interlocked.Increment(ref HostFor(connection.Host).PushesSent);
            connection.Push(frame);
        }

        internal Connection Connect(HostEndpointWithPort endPoint)
        {
            if (_network is not null)
            {
                throw new InvalidOperationException("This server takes stream connections. Call ConnectStream.");
            }

            var (host, seed) = Route(endPoint);
            var connection = new Connection(this, endPoint, host, seed);
            var state = HostFor(host);
            lock (state.Lock) state.Sessions.Add(connection);
            lock (_lock) _sessions.Add(connection);
            Interlocked.Increment(ref state.ConnectionsOpened);
            return connection;
        }

        private void Disconnect(Session session)
        {
            var state = HostFor(session.Host);
            lock (state.Lock) state.Sessions.Remove(session);
            lock (_lock) _sessions.Remove(session);
            Interlocked.Increment(ref state.ConnectionsClosed);
        }

        /// <summary>
        /// The open connections to the host, in the order they opened.
        /// </summary>
        public IReadOnlyList<Session> ConnectionsOn(string host)
        {
            lock (_lock) return _sessions.Where(c => c.Host == host && !c.IsClosed).ToList();
        }

        private HostState HostFor(string host)
        {
            lock (_lock)
            {
                if (!_hosts.TryGetValue(host, out var state))
                {
                    state = new HostState(host);
                    _hosts[host] = state;
                }
                return state;
            }
        }

        private void SetMap(string name, ServedMap served, string[] hosts)
        {
            lock (_lock)
            {
                var maps = MapsFor(name);
                Index(served);
                foreach (var host in hosts)
                {
                    maps.Nodes[host] = new NodeMap(served);
                }
            }
        }

        // The caller holds the lock. Every node serves the same nodes for one version of a map, so one body per version is read.
        private void Index(ServedMap map)
        {
            if (!map.HasAlternateAddresses || !_indexed.Add((map.Name, map.Version))) return;

            foreach (var node in map.Nodes)
            {
                foreach (var alternate in node.Alternates)
                {
                    _alternateHosts.Add(alternate.Host);
                    if (alternate.Kv > 0) _plainEndpoints[(alternate.Host, alternate.Kv)] = node.Name;
                    if (alternate.KvSsl > 0) _tlsEndpoints[(alternate.Host, alternate.KvSsl)] = node.Name;
                }
            }
        }

        private BucketMaps MapsFor(string name)
        {
            if (!_maps.TryGetValue(name, out var maps))
            {
                maps = new BucketMaps();
                _maps[name] = maps;
            }
            return maps;
        }

        /// <summary>
        /// Records a request the node has read. Call <see cref="Answer"/> for it next, now or once the host stops hanging.
        /// </summary>
        private void Receive(Session session, ReadOnlySpan<byte> packet)
        {
            var request = new Request(packet);
            var record = new ServerRequest(session.Host, session.Id, request.OpCode, session.Bucket,
                request.OpCode == OpCode.GetClusterConfig ? KnownVersion(request) : null, session.IsSeed);
            lock (_lock) _received.Add(record);
            Interlocked.Increment(ref HostFor(session.Host).RequestsReceived);
        }

        private SlicedMemoryOwner<byte> Answer(Session session, ReadOnlySpan<byte> packet)
        {
            Interlocked.Increment(ref _requests);
            Interlocked.Increment(ref HostFor(session.Host).RequestsAnswered);
            var request = new Request(packet);
            switch (request.OpCode)
            {
                case OpCode.Helo:
                    var features = new byte[request.Value.Length];
                    request.Value.CopyTo(features);
                    session.Negotiate(features);
                    return Response(request, ResponseStatus.Success, DataType.None, features);
                case OpCode.GetErrorMap:
                    return Response(request, ResponseStatus.Success, DataType.Json, ErrorMap);
                case OpCode.SaslList:
                    return Response(request, ResponseStatus.Success, DataType.None, SaslMechanisms);
                case OpCode.SelectBucket:
                    session.Bucket = Encoding.UTF8.GetString(request.Key.ToArray());
                    return Response(request, ResponseStatus.Success);
                case OpCode.GetCollectionsManifest:
                    return Response(request, ResponseStatus.Success, DataType.Json, Manifest);
                case OpCode.GetClusterConfig:
                    return ClusterConfig(session, request);
                default:
                    return DataOperation(session, request);
            }
        }

        private static ConfigVersion? KnownVersion(Request request) =>
            request.Extras.Length == 16
                ? new ConfigVersion(BinaryPrimitives.ReadUInt64BigEndian(request.Extras),
                    BinaryPrimitives.ReadUInt64BigEndian(request.Extras.Slice(8)))
                : null;

        private SlicedMemoryOwner<byte> ClusterConfig(Session connection, Request request)
        {
            var known = KnownVersion(request);

            lock (_lock)
            {
                var name = connection.Bucket ?? BucketConfig.GlobalBucketName;
                if (known is not null && !connection.Has(ServerFeatures.GetClusterConfigWithKnownVersion))
                {
                    return Response(request, ResponseStatus.InvalidArguments);
                }

                if (_answersLeft.TryGetValue(connection.Host, out var answersLeft))
                {
                    if (answersLeft == 0)
                    {
                        _fetches.Add(new ConfigFetch(connection.Host, connection.Bucket, known, null, connection.IsSeed));
                        return Response(request, ResponseStatus.TemporaryFailure);
                    }
                    _answersLeft[connection.Host] = answersLeft - 1;
                }

                var map = Serve(name, connection.Host)
                          ?? throw new InvalidOperationException($"No map of {name} is set for {connection.Host}.");

                // Like the server, answer with no map when the client already has this version or a newer one.
                var newer = known is null || map.Version > known.Value;
                _fetches.Add(new ConfigFetch(connection.Host, connection.Bucket, known, newer ? map.Version : null, connection.IsSeed));
                return newer
                    ? Response(request, ResponseStatus.Success, DataType.Json, map.Json)
                    : Response(request, ResponseStatus.Success);
            }
        }

        private ServedMap? Serve(string name, string host)
        {
            if (!_maps.TryGetValue(name, out var maps)) return null;
            if (!maps.Nodes.TryGetValue(host, out var node)) return maps.Default;

            if (node.Next is not null && node.FetchesBeforeNext-- <= 0)
            {
                node.Map = node.Next;
                node.Next = null;
            }
            return node.Map;
        }

        private SlicedMemoryOwner<byte> DataOperation(Session connection, Request request)
        {
            byte[]? body = null;
            lock (_lock)
            {
                if (_notMyVBucket.TryGetValue(connection.Host, out var bodies) && bodies.Count > 0)
                {
                    body = bodies.Dequeue();
                }
            }

            if (body is not null)
            {
                return Response(request, ResponseStatus.VBucketBelongsToAnotherServer, DataType.Json, body);
            }

            // The bucket holds no documents.
            return Response(request, request.OpCode == OpCode.NoOp ? ResponseStatus.Success : ResponseStatus.KeyNotFound);
        }

        private static SlicedMemoryOwner<byte> Response(Request request, ResponseStatus status,
            DataType dataType = DataType.None, ReadOnlySpan<byte> value = default) =>
            Frame(Magic.ClientResponse, (byte)request.OpCode, (short)status, request.Opaque, dataType, [], [], value);

        // The key is the bucket name, empty for the global map. The extras hold the epoch and the revision.
        private static SlicedMemoryOwner<byte> Notification(ConfigVersion version, string? bucket, DataType dataType,
            ReadOnlySpan<byte> value)
        {
            Span<byte> extras = stackalloc byte[16];
            BinaryPrimitives.WriteUInt64BigEndian(extras, version.Epoch);
            BinaryPrimitives.WriteUInt64BigEndian(extras.Slice(8), version.Revision);
            const byte clusterMapChangeNotification = 0x01;
            return Frame(Magic.ServerRequest, clusterMapChangeNotification, 0, 0, dataType, extras,
                Encoding.UTF8.GetBytes(bucket ?? string.Empty), value);
        }

        private static SlicedMemoryOwner<byte> Frame(Magic magic, byte opCode, short status, uint opaque, DataType dataType,
            ReadOnlySpan<byte> extras, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
        {
            var bodyLength = extras.Length + key.Length + value.Length;
            var frame = MemoryPool<byte>.Shared.RentAndSlice(OperationHeader.Length + bodyLength);
            var span = frame.Memory.Span;
            span.Slice(0, OperationHeader.Length).Clear();
            span[HeaderOffsets.Magic] = (byte)magic;
            span[HeaderOffsets.Opcode] = opCode;
            BinaryPrimitives.WriteUInt16BigEndian(span.Slice(HeaderOffsets.KeyLength), (ushort)key.Length);
            span[HeaderOffsets.ExtrasLength] = (byte)extras.Length;
            span[HeaderOffsets.Datatype] = (byte)dataType;
            BinaryPrimitives.WriteInt16BigEndian(span.Slice(HeaderOffsets.Status), status);
            BinaryPrimitives.WriteInt32BigEndian(span.Slice(HeaderOffsets.BodyLength), bodyLength);
            // The SDK writes the opaque little endian and the server echoes the bytes.
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(HeaderOffsets.Opaque), opaque);

            var body = span.Slice(OperationHeader.Length);
            extras.CopyTo(body);
            key.CopyTo(body.Slice(extras.Length));
            value.CopyTo(body.Slice(extras.Length + key.Length));
            return frame;
        }

        private readonly ref struct Request
        {
            public Request(ReadOnlySpan<byte> packet)
            {
                OpCode = (OpCode)packet[HeaderOffsets.Opcode];
                var framingExtrasLength = 0;
                int keyLength;
                if (packet[HeaderOffsets.Magic] == (byte)Magic.AltRequest)
                {
                    framingExtrasLength = packet[HeaderOffsets.FramingExtras];
                    keyLength = packet[HeaderOffsets.AltKeyLength];
                }
                else
                {
                    keyLength = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(HeaderOffsets.KeyLength));
                }

                var extrasLength = packet[HeaderOffsets.ExtrasLength];
                var bodyLength = BinaryPrimitives.ReadInt32BigEndian(packet.Slice(HeaderOffsets.BodyLength));
                Opaque = BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(HeaderOffsets.Opaque));

                var body = packet.Slice(OperationHeader.Length, bodyLength).Slice(framingExtrasLength);
                Extras = body.Slice(0, extrasLength);
                Key = body.Slice(extrasLength, keyLength);
                Value = body.Slice(extrasLength + keyLength);
            }

            public OpCode OpCode { get; }

            public uint Opaque { get; }

            public ReadOnlySpan<byte> Extras { get; }

            public ReadOnlySpan<byte> Key { get; }

            public ReadOnlySpan<byte> Value { get; }
        }

        /// <summary>
        /// A map in the form the nodes serve it.
        /// </summary>
        internal sealed class ServedMap(string name, byte[] json, ConfigVersion version)
        {
            private bool? _hasAlternateAddresses;
            private IReadOnlyList<ServedNode>? _nodes;

            public string Name { get; } = name;

            public byte[] Json { get; } = json;

            public ConfigVersion Version { get; } = version;

            internal bool HasAlternateAddresses =>
                _hasAlternateAddresses ??= Json.AsSpan().IndexOf("\"alternateAddresses\""u8) >= 0;

            /// <summary>
            /// The nodesExt entries that have a hostname, read from the JSON on first use.
            /// </summary>
            internal IReadOnlyList<ServedNode> Nodes => _nodes ??= ServedNode.Read(Json);

            public static ServedMap From(ClusterMapBuilder map)
            {
                var json = map.ToUtf8Bytes();
                var config = JsonSerializer.Deserialize(json, InternalSerializationContext.Default.BucketConfig)!;
                return new ServedMap(map.Name, json, config.ConfigVersion);
            }
        }

        /// <summary>
        /// The name of one nodesExt entry and the KV endpoints of its alternate addresses.
        /// </summary>
        internal sealed class ServedNode(string name, IReadOnlyList<(string Host, int Kv, int KvSsl)> alternates)
        {
            public string Name { get; } = name;

            public IReadOnlyList<(string Host, int Kv, int KvSsl)> Alternates { get; } = alternates;

            // Like the SDK, an alternate address with ports offers only those ports, and one without keeps the node ports.
            public static IReadOnlyList<ServedNode> Read(byte[] json)
            {
                // Only nodesExt is parsed, as the vbucket maps are most of the body.
                var reader = new Utf8JsonReader(json);
                reader.Read();
                while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                {
                    var isNodesExt = reader.ValueTextEquals("nodesExt"u8);
                    reader.Read();
                    if (!isNodesExt)
                    {
                        reader.Skip();
                        continue;
                    }

                    using var nodesExt = JsonDocument.ParseValue(ref reader);
                    return Read(nodesExt.RootElement);
                }
                return [];
            }

            private static List<ServedNode> Read(JsonElement nodesExt)
            {
                var nodes = new List<ServedNode>();
                foreach (var node in nodesExt.EnumerateArray())
                {
                    if (!node.TryGetProperty("hostname", out var hostname) || hostname.GetString() is not { } name) continue;

                    node.TryGetProperty("services", out var services);
                    var alternates = new List<(string, int, int)>();
                    if (node.TryGetProperty("alternateAddresses", out var networks))
                    {
                        foreach (var network in networks.EnumerateObject())
                        {
                            if (!network.Value.TryGetProperty("hostname", out var host) || host.GetString() is not { } alternateHost) continue;

                            var ports = network.Value.TryGetProperty("ports", out var alternatePorts) ? alternatePorts : services;
                            alternates.Add((alternateHost, Port(ports, "kv"), Port(ports, "kvSSL")));
                        }
                    }
                    nodes.Add(new ServedNode(name, alternates));
                }
                return nodes;
            }

            private static int Port(JsonElement ports, string key) =>
                ports.ValueKind == JsonValueKind.Object && ports.TryGetProperty(key, out var port) ? port.GetInt32() : 0;
        }

        private sealed class NodeMap(ServedMap map)
        {
            public ServedMap Map { get; set; } = map;

            public ServedMap? Next { get; set; }

            public int FetchesBeforeNext { get; set; }
        }

        private sealed class BucketMaps
        {
            public ServedMap? Default { get; set; }

            public Dictionary<string, NodeMap> Nodes { get; } = new(StringComparer.Ordinal);
        }

        /// <summary>
        /// What a node knows about one connection to it, in either mode.
        /// </summary>
        internal abstract class Session
        {
            private static int _lastId;

            private volatile HashSet<ServerFeatures> _features = new();
            private string? _bucket;

            protected Session(string host, HostEndpointWithPort address, bool isSeed)
            {
                Host = host;
                Address = address;
                IsSeed = isSeed;
                Id = Interlocked.Increment(ref _lastId);
            }

            /// <summary>
            /// The name of the node that serves the connection.
            /// </summary>
            public string Host { get; }

            /// <summary>
            /// The endpoint the SDK connected to. It is the alternate address on an external network.
            /// </summary>
            public HostEndpointWithPort Address { get; }

            /// <summary>
            /// True when <see cref="Address"/> matches no node, so the connection went to <see cref="SeedNode"/>.
            /// </summary>
            public bool IsSeed { get; }

            /// <summary>
            /// Tells the connections apart in <see cref="ServerRequest.Session"/>.
            /// </summary>
            public int Id { get; }

            /// <summary>
            /// The selected bucket, or null.
            /// </summary>
            public string? Bucket
            {
                get => Volatile.Read(ref _bucket);
                internal set => Volatile.Write(ref _bucket, value);
            }

            /// <summary>
            /// Requests the node read on this connection and holds unanswered, because the host hangs.
            /// </summary>
            public virtual int RepliesHeld => 0;

            /// <summary>
            /// Cluster map notifications the node holds for this connection, because the host hangs.
            /// </summary>
            public virtual int PushesHeld => 0;

            /// <summary>
            /// SDK writes on this connection that wait for room in the buffer to the node.
            /// </summary>
            public virtual int BlockedWrites => 0;

            public abstract bool IsClosed { get; }

            internal bool Has(ServerFeatures feature) => _features.Contains(feature);

            internal void Negotiate(byte[] features)
            {
                var negotiated = new HashSet<ServerFeatures>();
                for (var i = 0; i + 1 < features.Length; i += 2)
                {
                    negotiated.Add((ServerFeatures)BinaryPrimitives.ReadUInt16BigEndian(features.AsSpan(i)));
                }
                _features = negotiated;
            }

            /// <summary>
            /// Sends a server request frame to the client. The session owns the frame from here.
            /// </summary>
            internal abstract void Push(SlicedMemoryOwner<byte> frame);

            public override string ToString() =>
                $"{Host}{(IsSeed ? $" seed {Address}" : "")} #{Id} bucket={Bucket ?? "none"} held={RepliesHeld}";
        }

        /// <summary>
        /// One KV connection from a <see cref="ClusterNode"/> to a node of the fake server.
        /// Requests are answered before <see cref="SendAsync"/> returns.
        /// </summary>
        internal sealed class Connection : Session, IConnection
        {
            private static long _lastConnectionId;

            private readonly FakeClusterServer _server;
            private readonly CancellationTokenSource _closed = new();
            private int _disposed;

            internal Connection(FakeClusterServer server, HostEndpointWithPort endPoint, string host, bool isSeed)
                : base(host, endPoint, isSeed)
            {
                _server = server;
                EndPoint = new IPEndPoint(IPAddress.Loopback, endPoint.Port);
                ConnectionId = (ulong)Interlocked.Increment(ref _lastConnectionId);
                ContextId = ConnectionId.ToString("x16");
            }

            public override bool IsClosed => IsDead;

            public string ContextId { get; }

            public ulong ConnectionId { get; }

            public bool IsAuthenticated { get; set; }

            public bool IsSecure => false;

            public bool IsConnected => !IsDead;

            public EndPoint EndPoint { get; }

            public EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 0);

            public EndpointState EndpointState { get; set; } = EndpointState.Connected;

            public bool IsDead => Volatile.Read(ref _disposed) == 1;

            public CancellationToken ClosedToken => _closed.Token;

            public TimeSpan IdleTime => TimeSpan.Zero;

            public ServerFeatureSet ServerFeatures { get; set; } = ServerFeatureSet.Empty;

            public string? SupportedSaslMechanisms { get; set; }

            public string RemoteHost => Address.Host;

            public string LocalHost => "127.0.0.1";

            public Action<SlicedMemoryOwner<byte>>? OnClusterMapChangeNotification { get; set; }

            public ValueTask SendAsync(ReadOnlyMemory<byte> buffer, IOperation operation,
                CancellationToken cancellationToken = default)
            {
                if (IsDead)
                {
                    throw new SocketNotAvailableException($"The connection to {Host} is closed.");
                }

                _server.Receive(this, buffer.Span);
                operation.HandleOperationCompleted(_server.Answer(this, buffer.Span));
                return default;
            }

            public ValueTask CloseAsync(TimeSpan timeout)
            {
                Dispose();
                return default;
            }

            public void AddTags(IRequestSpan span)
            {
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 1) return;

                _server.Disconnect(this);
                _closed.Cancel();
                _closed.Dispose();
            }

            // The receive loop of a real connection hands server requests to the same callback.
            internal override void Push(SlicedMemoryOwner<byte> frame)
            {
                var callback = OnClusterMapChangeNotification;
                if (callback is null)
                {
                    frame.Dispose();
                    return;
                }
                callback(frame);
            }
        }
    }

    /// <summary>
    /// One GetClusterConfig request a node answered.
    /// </summary>
    internal sealed class ConfigFetch(string host, string? bucket, ConfigVersion? knownVersion, ConfigVersion? served, bool seed)
    {
        public string Host { get; } = host;

        /// <summary>
        /// The <see cref="FakeClusterServer.Session.IsSeed"/> of the connection.
        /// </summary>
        public bool Seed { get; } = seed;

        /// <summary>
        /// The bucket selected on the connection, or null for the global map.
        /// </summary>
        public string? Bucket { get; } = bucket;

        /// <summary>
        /// The version the client sent in the extras, or null when it asked for the map whatever the version.
        /// </summary>
        public ConfigVersion? KnownVersion { get; } = knownVersion;

        /// <summary>
        /// The version of the map in the answer, or null when the answer had no map.
        /// </summary>
        public ConfigVersion? Served { get; } = served;

        public override string ToString() =>
            $"{Host}{(Seed ? " seed" : "")} bucket={Bucket ?? "none"} known={KnownVersion?.ToString() ?? "none"} served={Served?.ToString() ?? "none"}";
    }

    /// <summary>
    /// One request a node received, answered or not.
    /// </summary>
    internal sealed class ServerRequest(string host, int session, OpCode opCode, string? bucket, ConfigVersion? knownVersion, bool seed)
    {
        public string Host { get; } = host;

        /// <summary>
        /// The <see cref="FakeClusterServer.Session.IsSeed"/> of the connection.
        /// </summary>
        public bool Seed { get; } = seed;

        /// <summary>
        /// The <see cref="FakeClusterServer.Session.Id"/> of the connection.
        /// </summary>
        public int Session { get; } = session;

        public OpCode OpCode { get; } = opCode;

        /// <summary>
        /// The bucket selected on the connection when the request came, or null.
        /// </summary>
        public string? Bucket { get; } = bucket;

        /// <summary>
        /// For a GetClusterConfig, the version the client sent in the extras, or null.
        /// </summary>
        public ConfigVersion? KnownVersion { get; } = knownVersion;

        public override string ToString() =>
            $"{Host}{(Seed ? " seed" : "")} #{Session} {OpCode} bucket={Bucket ?? "none"} known={KnownVersion?.ToString() ?? "none"}";
    }

    /// <summary>
    /// What happened on the connections to one host, read at one point in time.
    /// </summary>
    internal sealed class HostCounters
    {
        internal HostCounters(FakeClusterServer.HostState state, IReadOnlyList<FakeClusterServer.Session> open)
        {
            RequestsReceived = Interlocked.Read(ref state.RequestsReceived);
            RequestsAnswered = Interlocked.Read(ref state.RequestsAnswered);
            ConnectionsOpened = Interlocked.Read(ref state.ConnectionsOpened);
            ConnectionsClosed = Interlocked.Read(ref state.ConnectionsClosed);
            ConnectionsRefused = Interlocked.Read(ref state.ConnectionsRefused);
            PartialReadsByServer = Interlocked.Read(ref state.PartialReadsByServer);
            PartialReadsBySdk = Interlocked.Read(ref state.PartialReadsBySdk);
            PushesSent = Interlocked.Read(ref state.PushesSent);
            OpenConnections = open.Count;
            RepliesHeld = open.Sum(s => s.RepliesHeld);
            PushesHeld = open.Sum(s => s.PushesHeld);
            BlockedWrites = open.Sum(s => s.BlockedWrites);
        }

        public long RequestsReceived { get; }

        public long RequestsAnswered { get; }

        /// <summary>
        /// Requests the node read and holds unanswered, because the host hangs.
        /// </summary>
        public int RepliesHeld { get; }

        /// <summary>
        /// Cluster map notifications the node holds, because the host hangs.
        /// </summary>
        public int PushesHeld { get; }

        /// <summary>
        /// Cluster map notifications the node sent or holds, brief and full.
        /// </summary>
        public long PushesSent { get; }

        public long ConnectionsOpened { get; }

        /// <summary>
        /// Connections that ended, closed by either side.
        /// </summary>
        public long ConnectionsClosed { get; }

        /// <summary>
        /// Connects that failed because the host refuses connections.
        /// </summary>
        public long ConnectionsRefused { get; }

        public int OpenConnections { get; }

        /// <summary>
        /// SDK writes that wait for room in the buffer to the node.
        /// </summary>
        public int BlockedWrites { get; }

        /// <summary>
        /// Reads by the node that ended inside a request frame.
        /// </summary>
        public long PartialReadsByServer { get; }

        /// <summary>
        /// Reads by the SDK that ended inside a reply or notification frame.
        /// </summary>
        public long PartialReadsBySdk { get; }

        public override string ToString() =>
            $"received={RequestsReceived} answered={RequestsAnswered} held={RepliesHeld} pushes={PushesSent} pushesHeld={PushesHeld} " +
            $"opened={ConnectionsOpened} closed={ConnectionsClosed} refused={ConnectionsRefused} open={OpenConnections} " +
            $"blockedWrites={BlockedWrites}";
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
