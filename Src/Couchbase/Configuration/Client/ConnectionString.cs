using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using Couchbase.Annotations;
using Couchbase.Logging;

namespace Couchbase.Configuration.Client
{
    /// <summary>
    /// Represents a Couchbase cluster connection string.
    /// </summary>
    /// <remarks>
    /// In the future this should support each host having a separate port number to
    /// match the spec (https://github.com/couchbaselabs/sdk-rfcs/blob/master/rfc/0011-connection-string.md).
    /// However, the SDK currently can't work with different bootstrap ports per host,
    /// so we'll throw an error if we find it.
    /// </remarks>
    internal class ConnectionString : IEquatable<ConnectionString>
    {
        private static readonly ILog Log = LogManager.GetLogger<ConnectionString>();

        public ConnectionScheme Scheme { get; }
        public ReadOnlyCollection<string> Hosts { get; }
        public ushort? Port { get; }

        public ConnectionString(ConnectionScheme scheme, IEnumerable<string> hosts)
            : this(scheme, hosts, null)
        {
        }

        public ConnectionString(ConnectionScheme scheme, [NotNull] IEnumerable<string> hosts, ushort? port)
        {
            if (!Enum.IsDefined(typeof(ConnectionScheme), scheme))
            {
                #if NETSTANDARD15
                throw new ArgumentException(nameof(scheme), "Invalid connection scheme");
                #else
                throw new InvalidEnumArgumentException(nameof(scheme), (int) scheme, typeof(ConnectionScheme));
                #endif
            }
            if (hosts == null)
            {
                throw new ArgumentNullException(nameof(hosts));
            }

            Scheme = scheme;
            Hosts = new ReadOnlyCollection<string>(hosts.ToList());
            Port = port;
        }

        #region Equality

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != typeof(ConnectionString))
            {
                return false;
            }

            return Equals((ConnectionString) obj);
        }

        public bool Equals(ConnectionString other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (Scheme != other.Scheme || Port != other.Port || Hosts.Count != other.Hosts.Count)
            {
                return false;
            }

            for (var i = 0; i < Hosts.Count; i++)
            {
                if (Hosts[i] != other.Hosts[i])
                {
                    return false;
                }
            }

            return true;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int) Scheme;
                foreach (var host in Hosts)
                {
                    hashCode = (hashCode * 397) ^ host.GetHashCode();
                }

                hashCode = (hashCode * 397) ^ Port.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(ConnectionString left, ConnectionString right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ConnectionString left, ConnectionString right)
        {
            return !Equals(left, right);
        }

        #endregion

        #region Parsing

        /// <summary>
        /// Parses a connection string, throwing an exception if it is invalid.
        /// </summary>
        /// <param name="connectionString">Connection string to parse.</param>
        /// <returns>The parsed <see cref="ConnectionString"/>.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="Exception"></exception>
        public static ConnectionString Parse([NotNull] string connectionString)
        {
            if (connectionString == null)
            {
                throw new ArgumentNullException(nameof(connectionString));
            }

            var partsMatch = Regex.Match(connectionString,
                @"^((.*):\/\/)?(([^\/?:]*)(:([^\/?:@]*))?@)?([^\/?]*)(\/([^\?]*))?(\?(.*))?");
            if (!partsMatch.Success)
            {
                throw new Exception("Invalid connection string format");
            }

            var schemeString = partsMatch.Groups[2].Value;
            ConnectionScheme scheme;
            if (schemeString == "")
            {
                Log.Warn("Connection strings without a scheme (i.e. couchbase://) are deprecated");
                scheme = ConnectionScheme.Http;
            }
            else
            {
                if (!Enum.TryParse<ConnectionScheme>(schemeString, true, out scheme))
                {
                    throw new Exception($"Invalid scheme '{schemeString}' in connection string");
                }
            }

            var hostListMatch = Regex.Match(partsMatch.Groups[7].Value,
                @"([^;\,\:]+)(:([0-9]*))?(;\,)?");

            var hosts = new List<string>();
            ushort? port = null;
            while (hostListMatch.Success)
            {
                var hostName = hostListMatch.Groups[1].Value;
                if (hostName == "")
                {
                    throw new Exception("Invalid hostname in connection string");
                }

                hosts.Add(hostName);

                var portString = hostListMatch.Groups[3].Value;
                if (portString != "")
                {
                    if (!ushort.TryParse(portString, out var tempPort))
                    {
                        throw new Exception($"Invalid port '{portString}' in connection string");
                    }

                    if (port.HasValue && tempPort != port)
                    {
                        throw new Exception(
                            "Multiple port numbers in a connection string are not currently supported by this SDK");
                    }

                    port = tempPort;
                }

                hostListMatch = hostListMatch.NextMatch();
            }

            if (!hosts.Any())
            {
                throw new Exception("Connection string does not contain any hosts");
            }

            return new ConnectionString(scheme, hosts, port);
        }

        /// <summary>
        /// Attempts to parse a connection string.
        /// </summary>
        /// <param name="connectionString">Connection string to parse.</param>
        /// <param name="result">Parsed <see cref="ConnectionString"/> if the input was valid.  Otherwise null.</param>
        /// <returns>True if successfully parsed, otherwise false.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool TryParse([NotNull] string connectionString, out ConnectionString result)
        {
            if (connectionString == null)
            {
                throw new ArgumentNullException(nameof(connectionString));
            }

            try
            {
                result = Parse(connectionString);
                return true;
            }
            catch
            {
                result = null;
                return false;
            }
        }

        #endregion
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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

#endregion