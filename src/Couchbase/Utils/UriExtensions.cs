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
