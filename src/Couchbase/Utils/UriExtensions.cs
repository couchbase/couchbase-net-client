using System;
using Couchbase.Core.Configuration.Server;

namespace Couchbase.Utils
{
    internal static class UriExtensions
    {
        public static string Http = "http";
        public static string Https = "https";

        public static string QueryPath = "/query";
        public const string AnalyticsPath = "/analytics/service";

        internal static Uri GetQueryUri(this NodeAdapter nodeAdapter, ClusterOptions clusterOptions)
        {
            if (nodeAdapter.IsQueryNode)
            {
                return new UriBuilder
                {
                    Scheme = clusterOptions.EffectiveEnableTls ? Https : Http,
                    Host = nodeAdapter.Hostname,
                    Port = clusterOptions.EffectiveEnableTls ? nodeAdapter.N1QlSsl : nodeAdapter.N1Ql,
                    Path = QueryPath
                }.Uri;
            }

            return new UriBuilder
            {
                Scheme = clusterOptions.EffectiveEnableTls ? Https : Http,
                Host = nodeAdapter.Hostname,
            }.Uri;
        }

        internal static Uri GetAnalyticsUri(this NodeAdapter nodesAdapter, ClusterOptions clusterOptions)
        {
            if (nodesAdapter.IsAnalyticsNode)
            {
                return new UriBuilder
                {
                    Scheme = clusterOptions.EffectiveEnableTls ? Https : Http,
                    Host = nodesAdapter.Hostname,
                    Port = clusterOptions.EffectiveEnableTls ? nodesAdapter.AnalyticsSsl : nodesAdapter.Analytics,
                    Path = AnalyticsPath
                }.Uri;
            }
            return new UriBuilder
            {
                Scheme = clusterOptions.EffectiveEnableTls ? Https : Http,
                Host = nodesAdapter.Hostname,
            }.Uri;

        }

        internal static Uri GetSearchUri(this NodeAdapter nodeAdapter, ClusterOptions clusterOptions)
        {
            if (nodeAdapter.IsSearchNode)
            {
                return new UriBuilder
                {
                    Scheme = clusterOptions.EffectiveEnableTls ? Https : Http,
                    Host = nodeAdapter.Hostname,
                    Port = clusterOptions.EffectiveEnableTls ? nodeAdapter.FtsSsl : nodeAdapter.Fts
                }.Uri;
            }

            return new UriBuilder
            {
                Scheme = clusterOptions.EffectiveEnableTls ? Https : Http,
                Host = nodeAdapter.Hostname,
            }.Uri;
        }

        internal static Uri GetViewsUri(this NodeAdapter nodesAdapter, ClusterOptions clusterOptions)
        {
            if (nodesAdapter.IsKvNode)
            {
                return new UriBuilder
                {
                    Scheme = clusterOptions.EffectiveEnableTls ? Https : Http,
                    Host = nodesAdapter.Hostname,
                    Port = clusterOptions.EffectiveEnableTls ? nodesAdapter.ViewsSsl : nodesAdapter.Views
                }.Uri;
            }
            return new UriBuilder
            {
                Scheme = clusterOptions.EffectiveEnableTls ? Https : Http,
                Host = nodesAdapter.Hostname,
                Port = clusterOptions.EffectiveEnableTls ? nodesAdapter.ViewsSsl : nodesAdapter.Views
            }.Uri;
        }

        internal static Uri GetManagementUri(this NodeAdapter nodesAdapter, ClusterOptions clusterOptions)
        {
            return new UriBuilder
            {
                Scheme = clusterOptions.EffectiveEnableTls ? Https : Http,
                Host = nodesAdapter.Hostname,
                Port = clusterOptions.EffectiveEnableTls ? nodesAdapter.MgmtApiSsl : nodesAdapter.MgmtApi
            }.Uri;
        }
    }
}
