using Couchbase.Core.Compatibility;
using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.ConfigProfiles
{
    [InterfaceStability(Level.Volatile)]
    public record ConfigProfile(
        TimeSpan? KvConnectTimeout,
        TimeSpan? KvTimeout,
        TimeSpan? KvDurabilityTimeout,
        TimeSpan? ViewTimeout,
        TimeSpan? QueryTimeout,
        TimeSpan? AnalyticsTimeout,
        TimeSpan? SearchTimeout,
        TimeSpan? ManagementTimeout
        );

    [InterfaceStability(Level.Volatile)]
    public static class PreDefined
    {
        /// <summary>
        /// The default configuration values.
        /// </summary>
        public static readonly ConfigProfile Default = new(
            KvConnectTimeout: TimeSpan.FromSeconds(10),
            KvTimeout: TimeSpan.FromSeconds(2.5),
            KvDurabilityTimeout: TimeSpan.FromSeconds(10),
            ViewTimeout: TimeSpan.FromSeconds(75),
            QueryTimeout: TimeSpan.FromSeconds(75),
            AnalyticsTimeout: TimeSpan.FromSeconds(75),
            SearchTimeout: TimeSpan.FromSeconds(75),
            ManagementTimeout: TimeSpan.FromSeconds(75)
            );

        /// <summary>
        /// An empty profile used as a basis for profiles that only set a few values.
        /// </summary>
        public static readonly ConfigProfile NullProfile = new(
            KvConnectTimeout: null,
            KvTimeout: null,
            KvDurabilityTimeout: null,
            ViewTimeout: null,
            QueryTimeout: null,
            AnalyticsTimeout: null,
            SearchTimeout: null,
            ManagementTimeout: null
            );

        /// <summary>
        /// A profile for development (non-production) use over high-latency connections.
        /// </summary>
        public static readonly ConfigProfile WanDevelopment = NullProfile with
        {
            KvConnectTimeout = TimeSpan.FromSeconds(20),
            KvTimeout = TimeSpan.FromSeconds(20),
            KvDurabilityTimeout = TimeSpan.FromSeconds(20),
            ViewTimeout = TimeSpan.FromSeconds(120),
            QueryTimeout = TimeSpan.FromSeconds(120),
            AnalyticsTimeout = TimeSpan.FromSeconds(120),
            SearchTimeout = TimeSpan.FromSeconds(120),
            ManagementTimeout = TimeSpan.FromSeconds(120)
        };
    }
}
