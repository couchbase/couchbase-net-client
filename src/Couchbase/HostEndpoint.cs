using System;

#nullable enable

namespace Couchbase
{
    /// <summary>
    ///
    /// </summary>
    public readonly struct HostEndpoint : IEquatable<HostEndpoint>
    {
        /// <summary>
        /// Host name or IP address. IPv6 addresses should be wrapped in square braces.
        /// </summary>
        public string Host { get; }

        /// <summary>
        /// Port number, if any.
        /// </summary>
        public int? Port { get; }

        /// <summary>
        /// Creates a new HostEndpoint.
        /// </summary>
        /// <param name="host">Host name or IP address. IPv6 addresses should be wrapped in square braces.</param>
        /// <param name="port">Port number, if any.</param>
        public HostEndpoint(string host, int? port)
        {
            Host = host ?? throw new ArgumentNullException(nameof(host));
            Port = port;
        }

        public void Deconstruct(out string host)
        {
            host = Host;
        }

        public void Deconstruct(out string host, out int? port)
        {
            host = Host;
            port = Port;
        }

        /// <inheritdoc />
        public override string ToString() => Port != null ? $"{Host}:{Port}" : Host;

        #region Equality

        /// <inheritdoc />
        public bool Equals(HostEndpoint other)
        {
            return Host == other.Host && Port == other.Port;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is HostEndpoint other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (Host.GetHashCode() * 397) ^ Port.GetHashCode();
            }
        }

        public static bool operator ==(HostEndpoint left, HostEndpoint right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(HostEndpoint left, HostEndpoint right)
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
        public static HostEndpoint Parse(string server) =>
            (server ?? throw new ArgumentNullException(nameof(server)))
                .StartsWith("[", StringComparison.Ordinal) ?
                    ParseIpv6Server(server) :
                    ParseBasicServer(server);

        private static HostEndpoint ParseBasicServer(string server)
        {
            const int maxSplits = 2;
            var address = server.Split(':');
            if (address.Length > maxSplits)
            {
                throw new ArgumentException("Invalid server.", nameof(server));
            }

            if (address.Length == maxSplits)
            {
                if (!int.TryParse(address[1], out var port))
                {
                    throw new ArgumentException("Invalid port number.", nameof(server));
                }

                return new HostEndpoint(address[0], port);
            }
            else
            {
                return new HostEndpoint(address[0], null);
            }
        }

        private static HostEndpoint ParseIpv6Server(string server)
        {
            // Assumes an address with IPv6 syntax of "[ip]:port"
            // Since ip will contain colons, we can't just split the string

            const string invalidServer = "Not a valid IPv6 host/port string";

            var addressEnd = server.IndexOf(']', 1);
            if (addressEnd < 0)
            {
                throw new ArgumentException(invalidServer, nameof(server));
            }

            if (server.Length < addressEnd + 3 || server[addressEnd + 1] != ':')
            {
                // Doesn't have the port on the end
                return new HostEndpoint(server, null);
            }

            var address = server.Substring(0, addressEnd + 1);

            var portString = server.Substring(addressEnd + 2);
            if (!int.TryParse(portString, out var port))
            {
                throw new ArgumentException(invalidServer, nameof(server));
            }

            return new HostEndpoint(address, port);
        }

        #endregion
    }
}
