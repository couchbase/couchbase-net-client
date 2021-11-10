using System;
using Couchbase.Core.Configuration.Server;
using Couchbase.Utils;

#nullable enable

namespace Couchbase
{
    /// <summary>
    /// A host name and port pair. Unlike <see cref="HostEndpoint"/> this type requires a port number.
    /// </summary>
    internal readonly struct HostEndpointWithPort : IEquatable<HostEndpointWithPort>
#if NET6_0_OR_GREATER
        , ISpanFormattable
#endif
    {
        /// <summary>
        /// Host name or IP address. IPv6 addresses should be wrapped in square braces.
        /// </summary>
        public string Host { get; }

        /// <summary>
        /// Port number.
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// Creates a new HostEndpoint.
        /// </summary>
        /// <param name="host">Host name or IP address. IPv6 addresses should be wrapped in square braces.</param>
        /// <param name="port">Port number.</param>
        public HostEndpointWithPort(string host, int port)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (host == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(host));
            }

            Host = host;
            Port = port;
        }

        public void Deconstruct(out string host)
        {
            host = Host;
        }

        public void Deconstruct(out string host, out int port)
        {
            host = Host;
            port = Port;
        }

        /// <inheritdoc />
        public override string ToString() => FormattableString.Invariant($"{Host}:{Port}");

#if NET6_0_OR_GREATER

        /// <inheritdoc />
        public string ToString(string? format, IFormatProvider? formatProvider) => ToString();

        /// <inheritdoc />
        public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format,
            IFormatProvider? provider) =>
            destination.TryWrite($"{Host}:{Port}", out charsWritten);

#endif

        #region Equality

        /// <inheritdoc />
        public bool Equals(HostEndpointWithPort other)
        {
            return Host == other.Host && Port == other.Port;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is HostEndpointWithPort other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
#if NETSTANDARD2_0
            // Use ValueTuple to build the hash code from the components
            return (Host, Port).GetHashCode();
#else
            return HashCode.Combine(Host, Port);
#endif
        }

        public static bool operator ==(HostEndpointWithPort left, HostEndpointWithPort right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(HostEndpointWithPort left, HostEndpointWithPort right)
        {
            return !left.Equals(right);
        }

        #endregion

        #region Parse

        /// <summary>
        /// Parse a string in the format "server:11210" or "[::1]:11210".
        /// IPv6 addresses must be enclosed in square brackets.
        /// </summary>
        /// <param name="server">The server to parse.</param>
        /// <returns>The <see cref="HostEndpoint"/>.</returns>
        public static HostEndpointWithPort Parse(string server)
        {
            if (string.IsNullOrWhiteSpace(server))
            {
                ThrowHelper.ThrowArgumentNullException(nameof(server));
            }

            return server.StartsWith("[", StringComparison.Ordinal)
                ? ParseIpv6Server(server)
                : ParseBasicServer(server);
        }

        private static HostEndpointWithPort ParseBasicServer(string server)
        {
            const int expectedSplits = 2;
            var address = server.Split(':');
            if (address.Length != expectedSplits)
            {
                ThrowHelper.ThrowArgumentException("Invalid server.", nameof(server));
            }

            if (!int.TryParse(address[1], out var port))
            {
                ThrowHelper.ThrowArgumentException("Invalid port number.", nameof(server));
            }

            return new HostEndpointWithPort(address[0], port);
        }

        private static HostEndpointWithPort ParseIpv6Server(string server)
        {
            // Assumes an address with IPv6 syntax of "[ip]:port"
            // Since ip will contain colons, we can't just split the string

            const string invalidServer = "Not a valid IPv6 host/port string";

            var addressEnd = server.IndexOf(']', 1);
            if (addressEnd < 0)
            {
                ThrowHelper.ThrowArgumentException(invalidServer, nameof(server));
            }

            if (server.Length < addressEnd + 3 || server[addressEnd + 1] != ':')
            {
                // Doesn't have the port on the end
                ThrowHelper.ThrowArgumentException(invalidServer, nameof(server));
            }

            var address = server.Substring(0, addressEnd + 1);

            var portString = server.Substring(addressEnd + 2);
            if (!int.TryParse(portString, out var port))
            {
                ThrowHelper.ThrowArgumentException(invalidServer, nameof(server));
            }

            return new HostEndpointWithPort(address, port);
        }

        #endregion

        public static HostEndpointWithPort Create(NodeAdapter nodeAdapter, ClusterOptions options)
        {
            return new HostEndpointWithPort(nodeAdapter.Hostname,
                options.EffectiveEnableTls ? nodeAdapter.KeyValueSsl : nodeAdapter.KeyValue);
        }

        public static HostEndpointWithPort Create(NodesExt nodeExt, ClusterOptions options)
        {
            return new HostEndpointWithPort(nodeExt.Hostname,
                options.EffectiveEnableTls ? nodeExt.Services.KvSsl : nodeExt.Services.Kv);
        }

        public static HostEndpointWithPort Create(ExternalAddressesConfig extAddressConfig, ClusterOptions options)
        {
            return new HostEndpointWithPort(extAddressConfig.Hostname,
                options.EffectiveEnableTls ? extAddressConfig.Ports.KvSsl : extAddressConfig.Ports.Kv);
        }

        public static implicit operator HostEndpoint(HostEndpointWithPort hostEndpointWithPort) =>
            new(hostEndpointWithPort.Host, hostEndpointWithPort.Port);
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
