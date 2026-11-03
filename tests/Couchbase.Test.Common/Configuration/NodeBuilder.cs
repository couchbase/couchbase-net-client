#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Couchbase.Test.Common.Configuration
{
    /// <summary>
    /// Service ports, as the server writes them in nodesExt "services" and alternate address "ports".
    /// A zero port removes the entry, so a later revision can drop a service.
    /// </summary>
    internal abstract class ServicePortsBuilder<TSelf> where TSelf : ServicePortsBuilder<TSelf>
    {
        // The server writes the ports sorted by name.
        internal SortedDictionary<string, int> Ports { get; } = new(StringComparer.Ordinal);

        internal bool HasServices { get; private set; }

        public TSelf Mgmt(int port = 8091, int tlsPort = 18091) => Set("mgmt", port, "mgmtSSL", tlsPort);

        public virtual TSelf Kv(int port = 11210, int tlsPort = 11207) => Service("kv", port, "kvSSL", tlsPort);

        public TSelf Views(int port = 8092, int tlsPort = 18092) => Service("capi", port, "capiSSL", tlsPort);

        public TSelf Query(int port = 8093, int tlsPort = 18093) => Service("n1ql", port, "n1qlSSL", tlsPort);

        public TSelf Search(int port = 8094, int tlsPort = 18094) => Service("fts", port, "ftsSSL", tlsPort);

        public TSelf Analytics(int port = 8095, int tlsPort = 18095) => Service("cbas", port, "cbasSSL", tlsPort);

        public TSelf Eventing(int port = 8096, int tlsPort = 18096) =>
            Service("eventingAdminPort", port, "eventingSSL", tlsPort);

        internal int Port(string name) => Ports.TryGetValue(name, out var port) ? port : 0;

        internal void WritePorts(Utf8JsonWriter writer, string propertyName)
        {
            writer.WriteStartObject(propertyName);
            foreach (var entry in Ports)
            {
                writer.WriteNumber(entry.Key, entry.Value);
            }
            writer.WriteEndObject();
        }

        protected void SetPort(string name, int port)
        {
            if (port > 0) Ports[name] = port;
            else Ports.Remove(name);
        }

        protected void CopyTo(ServicePortsBuilder<TSelf> target)
        {
            foreach (var entry in Ports)
            {
                target.Ports[entry.Key] = entry.Value;
            }
            target.HasServices = HasServices;
        }

        protected void ReplacePorts(IEnumerable<KeyValuePair<string, int>> ports)
        {
            Ports.Clear();
            foreach (var entry in ports)
            {
                SetPort(entry.Key, entry.Value);
            }
            HasServices = true;
        }

        private TSelf Service(string name, int port, string tlsName, int tlsPort)
        {
            HasServices = true;
            return Set(name, port, tlsName, tlsPort);
        }

        private TSelf Set(string name, int port, string tlsName, int tlsPort)
        {
            SetPort(name, port);
            SetPort(tlsName, tlsPort);
            return (TSelf)this;
        }
    }

    /// <summary>
    /// One node of a <see cref="ClusterMapBuilder"/>. Management ports are always present.
    /// A node of a profile map starts with the first service set of the profile, and with the node UUID, server group
    /// and alternate addresses of its member.
    /// </summary>
    internal sealed class NodeBuilder : ServicePortsBuilder<NodeBuilder>
    {
        internal const string HostPlaceholder = "$HOST";

        private static readonly string[] NodesExtKeyOrder =
            ["services", "thisNode", "hostname", "nodeUUID", "serverGroup", "alternateAddresses"];

        private static readonly string[] LegacyKeyOrder = ["couchApiBase", "hostname", "ports", "alternateAddresses"];

        private readonly SortedDictionary<string, AlternateAddressBuilder> _alternateAddresses = new(StringComparer.Ordinal);
        private ClusterProfile? _profile;
        private string? _uuid;
        private string? _serverGroup;
        private bool _thisNode;

        internal NodeBuilder(string hostname)
        {
            Hostname = hostname;
            Mgmt();
        }

        /// <summary>
        /// A node of a profile map. The member is the index of a node of the capture in join order.
        /// </summary>
        internal NodeBuilder(string hostname, ClusterProfile profile, int member) : this(hostname)
        {
            _profile = profile;
            Member = member;
            _uuid = profile.NodeShape.NodeUuid(member);
            _serverGroup = profile.NodeShape.ServerGroup(member);
            if (profile.NodeShape.ServiceSets.Count > 0) Services(profile.NodeShape.ServiceSets[0].Services.ToArray());
        }

        internal string Hostname { get; }

        internal int? Member { get; private set; }

        internal bool IsKv => Ports.ContainsKey("kv");

        internal bool IsOutsideBucket { get; private set; }

        /// <summary>
        /// True when the node is in the bucket nodes[] list, serverList and vbucket map.
        /// </summary>
        internal bool IsInBucket => IsKv && !IsOutsideBucket;

        public NodeBuilder Uuid(string uuid)
        {
            _uuid = uuid;
            return this;
        }

        /// <summary>
        /// Adds the data service. Like a real data node it also gets view and projector ports.
        /// Call Views to change the view ports.
        /// </summary>
        public override NodeBuilder Kv(int port = 11210, int tlsPort = 11207)
        {
            base.Kv(port, tlsPort);
            AddOrRemoveWithKv("capi", 8092);
            AddOrRemoveWithKv("capiSSL", 18092);
            AddOrRemoveWithKv("projector", 9999);
            return this;
        }

        /// <summary>
        /// Sets the services of a profile node with the ports of the profile, for example Services("kv") for a node
        /// that joins or leaves. Management is always on. Like on a real data node, kv also brings the view and
        /// projector ports.
        /// </summary>
        public NodeBuilder Services(params string[] services)
        {
            var profile = _profile ?? throw new InvalidOperationException(
                "Services needs a profile map. Without a profile, use Kv, Query and the other service methods.");
            var ports = profile.NodeShape.Ports;
            var known = new HashSet<string>(ports.Keys.Select(ProfileNodeShape.ServiceOf), StringComparer.Ordinal);
            var unknown = services.Where(s => !known.Contains(s)).ToList();
            if (unknown.Count > 0)
            {
                throw new ArgumentException($"Profile {profile.Name} has no service {string.Join(", ", unknown)}. " +
                                            $"It has {string.Join(", ", known.OrderBy(s => s, StringComparer.Ordinal))}.", nameof(services));
            }

            var wanted = new HashSet<string>(services, StringComparer.Ordinal) { "mgmt" };
            if (wanted.Contains("kv"))
            {
                wanted.Add("capi");
                wanted.Add("projector");
            }
            ReplacePorts(ports.Where(p => wanted.Contains(ProfileNodeShape.ServiceOf(p.Key))));
            return this;
        }

        public NodeBuilder ThisNode(bool thisNode = true)
        {
            _thisNode = thisNode;
            return this;
        }

        public NodeBuilder ServerGroup(string group)
        {
            _serverGroup = group;
            return this;
        }

        /// <summary>
        /// Keeps the node in nodesExt but out of the bucket nodes[] list, serverList and vbucket map. This is a node
        /// that joined the cluster but not yet the bucket, or that left the bucket but not yet the cluster.
        /// The server lists such nodes last in nodesExt, so the builder does too.
        /// </summary>
        public NodeBuilder OutsideBucket(bool outside = true)
        {
            IsOutsideBucket = outside;
            return this;
        }

        /// <summary>
        /// Sets an alternate address. On a profile node it replaces the one the profile gives for the same network.
        /// </summary>
        public NodeBuilder AlternateAddress(string network, string hostname, Action<AlternateAddressBuilder>? ports = null)
        {
            var alternate = new AlternateAddressBuilder(hostname);
            ports?.Invoke(alternate);
            _alternateAddresses[network] = alternate;
            return this;
        }

        internal NodeBuilder Clone()
        {
            var clone = new NodeBuilder(Hostname)
            {
                _profile = _profile,
                Member = Member,
                IsOutsideBucket = IsOutsideBucket,
                _uuid = _uuid,
                _serverGroup = _serverGroup,
                _thisNode = _thisNode
            };
            CopyTo(clone);
            foreach (var entry in _alternateAddresses)
            {
                clone._alternateAddresses[entry.Key] = entry.Value.Clone();
            }
            return clone;
        }

        internal string HostPort(int port) =>
            Hostname.IndexOf(':') >= 0 ? $"[{Hostname}]:{port}" : $"{Hostname}:{port}";

        internal void WriteNodesExt(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();
            foreach (var key in ProfileJson.InOrder(_profile?.NodeShape.NodesExtKeyOrder, NodesExtKeyOrder))
            {
                switch (key)
                {
                    case "services":
                        WritePorts(writer, key);
                        break;
                    case "thisNode":
                        if (_thisNode) writer.WriteBoolean(key, true);
                        break;
                    case "hostname":
                        // A single node server leaves out the nodesExt hostname and writes $HOST everywhere else.
                        if (Hostname != HostPlaceholder) writer.WriteString(key, Hostname);
                        break;
                    case "nodeUUID":
                        if (_uuid is not null) writer.WriteString(key, _uuid);
                        break;
                    case "serverGroup":
                        if (_serverGroup is not null) writer.WriteString(key, _serverGroup);
                        break;
                    case "alternateAddresses":
                        WriteAlternates(writer, Alternates());
                        break;
                }
            }
            writer.WriteEndObject();
        }

        /// <summary>
        /// Writes the entry of the legacy "nodes" list, which holds only the data nodes of a bucket.
        /// </summary>
        internal void WriteLegacyNode(Utf8JsonWriter writer, string bucketName, string bucketUuid)
        {
            var shape = _profile?.NodeShape.LegacyNodes;
            writer.WriteStartObject();
            foreach (var key in ProfileJson.InOrder(shape?.KeyOrder, LegacyKeyOrder))
            {
                switch (key)
                {
                    case "couchApiBase":
                        if (Ports.ContainsKey("capi"))
                        {
                            writer.WriteString(key, $"http://{HostPort(Port("capi"))}/{bucketName}%2B{bucketUuid}");
                        }
                        break;
                    case "hostname":
                        writer.WriteString(key, HostPort(Port("mgmt")));
                        break;
                    case "ports":
                        writer.WriteStartObject(key);
                        writer.WriteNumber("direct", Port("kv"));
                        writer.WriteEndObject();
                        break;
                    case "alternateAddresses":
                        if (shape is not null) WriteLegacyAlternates(writer, shape);
                        break;
                }
            }
            writer.WriteEndObject();
        }

        /// <summary>
        /// The alternate addresses of the profile, replaced or extended by the ones set on the node.
        /// </summary>
        private List<Alternate> Alternates()
        {
            var result = new List<Alternate>();
            if (_profile is not null && Member is { } member)
            {
                foreach (var network in _profile.NodeShape.AlternateAddresses)
                {
                    if (_alternateAddresses.ContainsKey(network.Network)) continue;
                    var hostname = network.Hostname(member) ?? Hostname.Replace(".example.com", $".{network.Network}.example.com");
                    var ports = new List<KeyValuePair<string, int>>();
                    foreach (var key in network.KeyOrder)
                    {
                        if (network.PortsFollowServices && !Ports.ContainsKey(key)) continue;
                        var port = (network.Ports.TryGetValue(key, out var profilePort) ? profilePort.Value(member) : null) ?? Port(key);
                        if (port > 0) ports.Add(new KeyValuePair<string, int>(key, port));
                    }
                    result.Add(new Alternate(network.Network, hostname, ports));
                }
            }

            foreach (var entry in _alternateAddresses)
            {
                result.Add(new Alternate(entry.Key, entry.Value.Hostname, entry.Value.Ports.ToList()));
            }
            return result;
        }

        private static void WriteAlternates(Utf8JsonWriter writer, List<Alternate> alternates)
        {
            if (alternates.Count == 0) return;
            writer.WriteStartObject("alternateAddresses");
            foreach (var alternate in alternates)
            {
                writer.WriteStartObject(alternate.Network);
                writer.WriteString("hostname", alternate.Hostname);
                WritePortList(writer, alternate.Ports);
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }

        /// <summary>
        /// A nodes[] entry has the alternate host name of the node and only the ports the profile lists.
        /// </summary>
        private void WriteLegacyAlternates(Utf8JsonWriter writer, ProfileLegacyNodes shape)
        {
            var alternates = Alternates();
            var started = false;
            foreach (var network in shape.AlternateAddresses)
            {
                var alternate = alternates.Find(a => a.Network == network.Network);
                if (alternate is null) continue;
                if (!started)
                {
                    writer.WriteStartObject("alternateAddresses");
                    started = true;
                }
                writer.WriteStartObject(network.Network);
                writer.WriteString("hostname", alternate.Hostname);
                WritePortList(writer, alternate.Ports.Where(p => network.PortKeys.Contains(p.Key)).ToList());
                writer.WriteEndObject();
            }
            if (started) writer.WriteEndObject();
        }

        private static void WritePortList(Utf8JsonWriter writer, List<KeyValuePair<string, int>> ports)
        {
            if (ports.Count == 0) return;
            writer.WriteStartObject("ports");
            foreach (var entry in ports)
            {
                writer.WriteNumber(entry.Key, entry.Value);
            }
            writer.WriteEndObject();
        }

        private void AddOrRemoveWithKv(string name, int defaultPort)
        {
            if (!IsKv) Ports.Remove(name);
            else if (!Ports.ContainsKey(name)) Ports[name] = defaultPort;
        }

        private sealed class Alternate(string network, string hostname, List<KeyValuePair<string, int>> ports)
        {
            public string Network { get; } = network;

            public string Hostname { get; } = hostname;

            public List<KeyValuePair<string, int>> Ports { get; } = ports;
        }
    }

    /// <summary>
    /// An alternate address of a node. It lists only the ports set here, like the server does.
    /// </summary>
    internal sealed class AlternateAddressBuilder : ServicePortsBuilder<AlternateAddressBuilder>
    {
        internal AlternateAddressBuilder(string hostname)
        {
            Hostname = hostname;
        }

        internal string Hostname { get; }

        internal AlternateAddressBuilder Clone()
        {
            var clone = new AlternateAddressBuilder(Hostname);
            CopyTo(clone);
            return clone;
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
