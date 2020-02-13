using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

#nullable enable

namespace Couchbase
{
    internal class ConnectionString
    {
        private const int KeyValuePort = 11210;
        private const int SecureKeyValuePort = 11207;

        private static readonly Regex ConnectionStringRegex = new Regex(
            "^((?<scheme>[^://]+)://)?((?<username>[^\n@]+)@)?(?<hosts>[^\n?]+)?(\\?(?<params>(.+)))?",
            RegexOptions.Compiled | RegexOptions.CultureInvariant
        );

        private static readonly Regex Ipv6Regex = new Regex(
            "^\\[(?<address>.+)](:?(?<port>[0-9]+))?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant
        );

        public Scheme Scheme { get; private set; } = Scheme.Couchbase;
        public string? Username { get; private set; }
        public IList<HostEndpoint> Hosts { get; private set; } = new List<HostEndpoint>();
        public IDictionary<string, string> Parameters { get; private set; } = new Dictionary<string, string>();

        private ConnectionString()
        {
        }

        public ConnectionString(ConnectionString source, IEnumerable<HostEndpoint> newHosts)
        {
            Scheme = source.Scheme;
            Username = source.Username;
            Hosts = newHosts.ToList();
            Parameters = source.Parameters;
        }

        internal static ConnectionString Parse(string input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            var match = ConnectionStringRegex.Match(input);
            if (!match.Success)
            {
                throw new ArgumentException("Invalid connection string");
            }

            var connectionString = new ConnectionString();

            if (match.Groups["scheme"].Success)
            {
                switch (match.Groups["scheme"].Value)
                {
                    case "couchbase":
                        connectionString.Scheme = Scheme.Couchbase;
                        break;
                    // ReSharper disable once StringLiteralTypo
                    case "couchbases":
                        connectionString.Scheme = Scheme.Couchbases;
                        break;
                    case "http":
                        connectionString.Scheme = Scheme.Http;
                        break;
                    default:
                        throw new ArgumentException($"Unknown scheme {match.Groups["scheme"].Value}");
                }
            }

            if (match.Groups["username"].Success)
            {
                connectionString.Username = match.Groups["username"].Value;
            }

            if (match.Groups["hosts"].Success)
            {
                connectionString.Hosts = match.Groups["hosts"].Value.Split(',')
                    .Select(host => HostEndpoint.Parse(host.Trim()))
                    .ToList();
            }

            if (match.Groups["params"].Success)
            {
                connectionString.Parameters = match.Groups["params"].Value.Split('&')
                    .Select(x =>
                    {
                        var kvp = x.Split('=');
                        return new KeyValuePair<string, string>(kvp[0], kvp[1]);
                    })
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }

            return connectionString;
        }

        public IEnumerable<HostEndpoint> GetBootstrapEndpoints()
        {
            foreach (var endpoint in Hosts)
            {
                if (endpoint.Port != null)
                {
                    yield return endpoint;
                }
                else
                {
                    yield return new HostEndpoint(endpoint.Host,
                        Scheme == Scheme.Couchbases ? SecureKeyValuePort : KeyValuePort);
                }
            }
        }

        internal Uri GetDnsBootStrapUri()
        {
            return new UriBuilder
            {
                Scheme = Scheme.ToString(),
                Host = Hosts.First().Host
            }.Uri;
        }

        /// <summary>
        /// Identifies if this connection string is valid for use with DNS SRV lookup.
        /// </summary>
        /// <returns>True if valid for DNS SRV lookup.</returns>
        /// <seealso cref="GetDnsBootStrapUri"/>.
        public bool IsValidDnsSrv()
        {
            if (Scheme != Scheme.Couchbase && Scheme != Scheme.Couchbases)
            {
                return false;
            }

            if (Hosts.Count > 1)
            {
                return false;
            }

            return Hosts.Single().Port == null;
        }

        public override string ToString()
        {
            var builder = new StringBuilder();

            builder.Append(Scheme switch
            {
                Scheme.Couchbase => "couchbase://",
                Scheme.Couchbases => "couchbases://",
                _ => "http://"
            });

            if (!string.IsNullOrEmpty(Username))
            {
                builder.Append(Uri.EscapeDataString(Username));
                builder.Append('@');
            }

            for (var hostIndex = 0; hostIndex < Hosts.Count; hostIndex++)
            {
                if (hostIndex > 0)
                {
                    builder.Append(',');
                }

                builder.Append(Hosts[hostIndex].Host);
                if (Hosts[hostIndex].Port != null)
                {
                    builder.AppendFormat(":{0}", Hosts[hostIndex].Port);
                }
            }

            if (Parameters.Count > 0)
            {
                var first = true;
                foreach (var parameter in Parameters)
                {
                    if (first)
                    {
                        first = false;
                        builder.Append('?');
                    }
                    else
                    {
                        builder.Append('&');
                    }

                    builder.Append(Uri.EscapeDataString(parameter.Key));
                    builder.Append('=');
                    builder.Append(Uri.EscapeDataString(parameter.Value));
                }
            }

            return builder.ToString();
        }
    }

    internal enum Scheme
    {
        Http,
        Couchbase,
        // ReSharper disable once IdentifierTypo
        Couchbases
    }
}
