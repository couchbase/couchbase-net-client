using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Couchbase.IntegrationTests.Utils
{
    /// <summary>
    /// A <c>Fact</c> that may be skipped due to the CB_SERVER_VERSION environment variable.
    /// </summary>
    /// <remarks>
    /// Checking against versions of irregular format requires use of the ExplicitDeny or ExplicitAllow parameters.
    /// </remarks>
    public class CouchbaseHasReplicasFact : FactAttribute
    {
        public static Lazy<int> NumReplicas = new(() =>
        {
            var numReplicas = 0;

            var clusterOptions = new ConfigurationBuilder()
                .AddJsonFile("config.json")
                .Build()
                .GetSection("couchbase")
                .Get<ClusterOptions>();

            var connectionString = clusterOptions.ConnectionString ?? $"http:localhost";
            var versionEndpoint = new UriBuilder(connectionString);
            versionEndpoint.Scheme = "http";
            versionEndpoint.Path = "pools/default/buckets/" +
                                   (clusterOptions.Buckets.Count > 0 ? clusterOptions.Buckets[0] : "default"); // assume bucket name
            versionEndpoint.Query = string.Empty;
            versionEndpoint.Port = 8091;
#pragma warning disable CS0618 // Type or member is obsolete
            versionEndpoint.UserName = clusterOptions.UserName ?? versionEndpoint.UserName;
            versionEndpoint.Password = clusterOptions.Password ?? versionEndpoint.Password;

            using var httpClient = new HttpClient();
            var authString = $"{clusterOptions.UserName}:{clusterOptions.Password}";
#pragma warning restore CS0618 // Type or member is obsolete
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", base64);
            var responseJsonString = httpClient.GetStringAsync(versionEndpoint.Uri).Result;
            var parsedJson = JsonDocument.Parse(responseJsonString);

            var vBucketServerMap = parsedJson.RootElement.GetProperty("vBucketServerMap");
            numReplicas = vBucketServerMap.GetProperty("numReplicas").GetInt32();

            return numReplicas;
        });

        /// <summary>
        /// Gets or sets the minimum Version necessary to run the test.
        /// </summary>
        /// <remarks>If it does not parse as a Version, it will be ignored.</remarks>
        public int MinNumReplicas { get; set; } = -1;

        /// <summary>
        /// Gets or sets the maximum Version necessary to run the test.
        /// </summary>
        /// <remarks>If it does not parse as a Version, it will be ignored.</remarks>
        public int MaxNumReplicas { get; set; } = -1;

        /// <summary>
        /// Gets or sets the list of versions that will be considered as no-skip. Supercedes everything but the Skip parameter itself.
        /// </summary>
        public int[] ExplicitAllow { get; set; }

        /// <summary>
        /// Gets or sets the list of versions that will be skipped. Supercedes MinVersion and MaxVersion.
        /// </summary>
        public int[] ExplicitSkip { get; set; }

        /// <summary>
        /// Gets or sets a value indicating this test should be skipped.  Supercedes all other version checking.
        /// </summary>
        public override string Skip
        {
            get => SkipBasedOnNumReplicas(base.Skip, ExplicitAllow, ExplicitSkip, MinNumReplicas,
                MaxNumReplicas);
            set => base.Skip = value;
        }


        internal static string SkipBasedOnNumReplicas(string baseSkip, int[] explicitAllowVersions,
            int[] explicitSkipVersions, int minNumReplicas, int maxNumReplicas)
        {
            var numReplicas = NumReplicas.Value;

            var explicitAllow = new HashSet<int>(explicitAllowVersions ?? Array.Empty<int>());
            var explicitDeny = new HashSet<int>(explicitSkipVersions ?? Array.Empty<int>());
            if (explicitAllow.Contains(numReplicas))
            {
                return null;
            }

            if (explicitDeny.Contains(numReplicas))
            {
                return $"Version {numReplicas} is in the Explicit Skip list.";
            }

            if (minNumReplicas != -1 && numReplicas < minNumReplicas)
            {
                return $"NumReplicas {numReplicas} was below {minNumReplicas}.";
            }

            if (maxNumReplicas != -1 && numReplicas > maxNumReplicas)
            {
                return $"NumReplicas {numReplicas} was above {maxNumReplicas}.";
            }

            return baseSkip;
        }
    }
}