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
    /// A host is the address the SDK connects to, so it is the alternate address on an external network.
    /// HELLO grants every feature the SDK asks for. A connection serves the map of the bucket it selected,
    /// or the global map when it selected none.
    /// </remarks>
    internal sealed class FakeClusterServer
    {
        private static readonly byte[] ErrorMap = Encoding.UTF8.GetBytes("""{"version":2,"revision":1,"errors":{}}""");
        private static readonly byte[] SaslMechanisms = Encoding.UTF8.GetBytes("SCRAM-SHA512 SCRAM-SHA256");
        private static readonly byte[] Manifest = Encoding.UTF8.GetBytes(
            """{"uid":"0","scopes":[{"name":"_default","uid":"0","collections":[{"name":"_default","uid":"0"}]}]}""");

        private readonly object _lock = new();
        private readonly Dictionary<string, BucketMaps> _maps = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _answersLeft = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Queue<byte[]>> _notMyVBucket = new(StringComparer.Ordinal);
        private readonly List<ConfigFetch> _fetches = new();
        private readonly List<Connection> _connections = new();

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

        public void ClearFetches()
        {
            lock (_lock) _fetches.Clear();
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

                connection.Push(Notification(served.Version, bucket, DataType.Json, served.Json));
            }
        }

        /// <summary>
        /// The node pushes only the version, to each connection that negotiated brief notifications.
        /// </summary>
        public void PushBrief(string host, ulong epoch, ulong rev)
        {
            foreach (var connection in ConnectionsOn(host))
            {
                if (!connection.Has(ServerFeatures.ClustermapChangeNotificationBrief)) continue;

                connection.Push(Notification(new ConfigVersion(epoch, rev), connection.Bucket, DataType.None, []));
            }
        }

        internal Connection Connect(HostEndpointWithPort endPoint)
        {
            var connection = new Connection(this, endPoint);
            lock (_lock) _connections.Add(connection);
            return connection;
        }

        private void Disconnect(Connection connection)
        {
            lock (_lock) _connections.Remove(connection);
        }

        private void SetMap(string name, ServedMap served, string[] hosts)
        {
            lock (_lock)
            {
                var maps = MapsFor(name);
                foreach (var host in hosts)
                {
                    maps.Nodes[host] = new NodeMap(served);
                }
            }
        }

        private List<Connection> ConnectionsOn(string host)
        {
            lock (_lock) return _connections.Where(c => c.Host == host).ToList();
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

        private SlicedMemoryOwner<byte> Answer(Connection connection, ReadOnlySpan<byte> packet)
        {
            var request = new Request(packet);
            switch (request.OpCode)
            {
                case OpCode.Helo:
                    var features = new byte[request.Value.Length];
                    request.Value.CopyTo(features);
                    connection.Negotiate(features);
                    return Response(request, ResponseStatus.Success, DataType.None, features);
                case OpCode.GetErrorMap:
                    return Response(request, ResponseStatus.Success, DataType.Json, ErrorMap);
                case OpCode.SaslList:
                    return Response(request, ResponseStatus.Success, DataType.None, SaslMechanisms);
                case OpCode.SelectBucket:
                    connection.Bucket = Encoding.UTF8.GetString(request.Key.ToArray());
                    return Response(request, ResponseStatus.Success);
                case OpCode.GetCollectionsManifest:
                    return Response(request, ResponseStatus.Success, DataType.Json, Manifest);
                case OpCode.GetClusterConfig:
                    return ClusterConfig(connection, request);
                default:
                    return DataOperation(connection, request);
            }
        }

        private SlicedMemoryOwner<byte> ClusterConfig(Connection connection, Request request)
        {
            ConfigVersion? known = request.Extras.Length == 16
                ? new ConfigVersion(BinaryPrimitives.ReadUInt64BigEndian(request.Extras),
                    BinaryPrimitives.ReadUInt64BigEndian(request.Extras.Slice(8)))
                : null;

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
                        _fetches.Add(new ConfigFetch(connection.Host, connection.Bucket, known, null));
                        return Response(request, ResponseStatus.TemporaryFailure);
                    }
                    _answersLeft[connection.Host] = answersLeft - 1;
                }

                var map = Serve(name, connection.Host)
                          ?? throw new InvalidOperationException($"No map of {name} is set for {connection.Host}.");

                // Like the server, answer with no map when the client already has this version or a newer one.
                var newer = known is null || map.Version > known.Value;
                _fetches.Add(new ConfigFetch(connection.Host, connection.Bucket, known, newer ? map.Version : null));
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

        private SlicedMemoryOwner<byte> DataOperation(Connection connection, Request request)
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
            public string Name { get; } = name;

            public byte[] Json { get; } = json;

            public ConfigVersion Version { get; } = version;

            public static ServedMap From(ClusterMapBuilder map)
            {
                var json = map.ToUtf8Bytes();
                var config = JsonSerializer.Deserialize(json, InternalSerializationContext.Default.BucketConfig)!;
                return new ServedMap(map.Name, json, config.ConfigVersion);
            }
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
        /// One KV connection from a <see cref="ClusterNode"/> to a node of the fake server.
        /// Requests are answered before <see cref="SendAsync"/> returns.
        /// </summary>
        internal sealed class Connection : IConnection
        {
            private static long _lastConnectionId;

            private readonly FakeClusterServer _server;
            private readonly CancellationTokenSource _closed = new();
            private volatile HashSet<ServerFeatures> _features = new();
            private string? _bucket;
            private int _disposed;

            internal Connection(FakeClusterServer server, HostEndpointWithPort endPoint)
            {
                _server = server;
                Host = endPoint.Host;
                EndPoint = new IPEndPoint(IPAddress.Loopback, endPoint.Port);
                ConnectionId = (ulong)Interlocked.Increment(ref _lastConnectionId);
                ContextId = ConnectionId.ToString("x16");
            }

            public string Host { get; }

            /// <summary>
            /// The selected bucket, or null.
            /// </summary>
            internal string? Bucket
            {
                get => Volatile.Read(ref _bucket);
                set => Volatile.Write(ref _bucket, value);
            }

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

            public string RemoteHost => Host;

            public string LocalHost => "127.0.0.1";

            public Action<SlicedMemoryOwner<byte>>? OnClusterMapChangeNotification { get; set; }

            public ValueTask SendAsync(ReadOnlyMemory<byte> buffer, IOperation operation,
                CancellationToken cancellationToken = default)
            {
                if (IsDead)
                {
                    throw new SocketNotAvailableException($"The connection to {Host} is closed.");
                }

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

            // The receive loop of a real connection hands server requests to the same callback.
            internal void Push(SlicedMemoryOwner<byte> frame)
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
    internal sealed class ConfigFetch(string host, string? bucket, ConfigVersion? knownVersion, ConfigVersion? served)
    {
        public string Host { get; } = host;

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
            $"{Host} bucket={Bucket ?? "none"} known={KnownVersion?.ToString() ?? "none"} served={Served?.ToString() ?? "none"}";
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
