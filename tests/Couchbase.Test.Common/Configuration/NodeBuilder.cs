#nullable enable
using System;
using System.Collections.Generic;
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
    /// </summary>
    internal sealed class NodeBuilder : ServicePortsBuilder<NodeBuilder>
    {
        internal const string HostPlaceholder = "$HOST";

        private readonly SortedDictionary<string, AlternateAddressBuilder> _alternateAddresses = new(StringComparer.Ordinal);
        private string? _uuid;
        private string? _serverGroup;
        private bool _thisNode;

        internal NodeBuilder(string hostname)
        {
            Hostname = hostname;
            Mgmt();
        }

        internal string Hostname { get; }

        internal bool IsKv => Ports.ContainsKey("kv");

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

        public NodeBuilder AlternateAddress(string network, string hostname, Action<AlternateAddressBuilder>? ports = null)
        {
            var alternate = new AlternateAddressBuilder(hostname);
            ports?.Invoke(alternate);
            _alternateAddresses[network] = alternate;
            return this;
        }

        internal NodeBuilder Clone()
        {
            var clone = new NodeBuilder(Hostname) { _uuid = _uuid, _serverGroup = _serverGroup, _thisNode = _thisNode };
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
            WritePorts(writer, "services");
            if (_thisNode) writer.WriteBoolean("thisNode", true);

            // A single node server leaves out the nodesExt hostname and writes $HOST everywhere else.
            if (Hostname != HostPlaceholder) writer.WriteString("hostname", Hostname);
            if (_uuid is not null) writer.WriteString("nodeUUID", _uuid);
            if (_serverGroup is not null) writer.WriteString("serverGroup", _serverGroup);

            if (_alternateAddresses.Count > 0)
            {
                writer.WriteStartObject("alternateAddresses");
                foreach (var entry in _alternateAddresses)
                {
                    writer.WriteStartObject(entry.Key);
                    writer.WriteString("hostname", entry.Value.Hostname);
                    if (entry.Value.Ports.Count > 0) entry.Value.WritePorts(writer, "ports");
                    writer.WriteEndObject();
                }
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        /// <summary>
        /// Writes the entry of the legacy "nodes" list, which holds only the data nodes of a bucket.
        /// </summary>
        internal void WriteLegacyNode(Utf8JsonWriter writer, string bucketName, string bucketUuid)
        {
            writer.WriteStartObject();
            if (Ports.ContainsKey("capi"))
            {
                writer.WriteString("couchApiBase", $"http://{HostPort(Port("capi"))}/{bucketName}%2B{bucketUuid}");
            }
            writer.WriteString("hostname", HostPort(Port("mgmt")));
            writer.WriteStartObject("ports");
            writer.WriteNumber("direct", Port("kv"));
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        private void AddOrRemoveWithKv(string name, int defaultPort)
        {
            if (!IsKv) Ports.Remove(name);
            else if (!Ports.ContainsKey(name)) Ports[name] = defaultPort;
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
