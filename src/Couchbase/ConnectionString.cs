using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace Couchbase
{
    internal class ConnectionString
    {
        private static readonly Regex ConnectionStringRegex = new Regex(
            "^((?<scheme>[^://]+)://)?((?<username>[^\n@]+)@)?(?<hosts>[^\n?]+)?(\\?(?<params>(.+)))?",
            RegexOptions.Compiled | RegexOptions.CultureInvariant
        );

        private static readonly Regex Ipv6Regex = new Regex(
            "^\\[(?<address>.+)](:?(?<port>[0-9]+))?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant
        );

        internal Scheme Scheme { get; private set; } = Scheme.Couchbase;
        internal string Username { get; private set; }
        internal IList<string> Hosts { get; private set; } = new List<string>();
        internal IDictionary<string, string> Parameters { get; private set; } = new Dictionary<string, string>();

        internal static ConnectionString Parse(string input)
        {
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
                    .Select(host => host.Trim())
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
    }

    internal enum Scheme
    {
        Http,
        Couchbase,
        Couchbases
    }
}
