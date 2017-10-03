using System;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.N1QL;

namespace Couchbase.Utils
{
    internal static class UrlUtil
    {
        public static string Http = "http";
        public static string Https = "https";
        // ReSharper disable once InconsistentNaming
        public static string N1QLUriFormat = "{0}://{1}:{2}/query";
        public static string ViewUriFormat = "{0}://{1}:{2}/{3}/";
        public static string BaseUriFormat = "{0}://{1}:{2}/pools";
        public static string SearchUriFormat = "{0}//{1}:{2}";
        public const string AnalyticsUriFormat = "{0}://{1}:{2}/analytics/service";

        public static Uri GetBaseUri(INodeAdapter adapter, BucketConfiguration config)
        {
            return new Uri(GetBaseUriAsString(adapter, config));
        }

        public static Uri GetViewBaseUri(INodeAdapter adapter, BucketConfiguration config)
        {
            return new Uri(GetViewBaseUriAsString(adapter, config));
        }

        public static FailureCountingUri GetFailureCountingBaseUri(INodeAdapter adapter, BucketConfiguration config)
        {
            return new FailureCountingUri(GetN1QLBaseUriAsString(adapter, config));
        }


        public static FailureCountingUri GetFailureCountinSearchBaseUri(INodeAdapter adapter,
            BucketConfiguration config)
        {
            return new FailureCountingUri(GetSearchBaseUri(adapter, config));
        }

        public static FailureCountingUri GetFailureCountingAnalyticsUri(INodeAdapter adapter, BucketConfiguration config)
        {
            return new FailureCountingUri(GetAnalyticsUri(adapter, config));
        }

        public static string GetSearchBaseUri(INodeAdapter adapter,
            BucketConfiguration config)
        {
            var uriAsString = string.Format(BaseUriFormat,
                config.UseSsl ? Https : Http,
                adapter.Hostname,
                config.UseSsl ? adapter.FtsSsl : adapter.Fts);

            return uriAsString;
        }

        // ReSharper disable once InconsistentNaming
        public static Uri GetN1QLBaseUri(INodeAdapter adapter, BucketConfiguration config)
        {
            return new Uri(GetN1QLBaseUriAsString(adapter, config));
        }

        public static string GetViewBaseUriAsString(INodeAdapter adapter, BucketConfiguration config)
        {
            var uriAsString = string.Format(ViewUriFormat,
                config.UseSsl ? Https : Http,
                adapter.Hostname,
                config.UseSsl ? adapter.ViewsSsl : adapter.Views,
                config.BucketName);

            return uriAsString;
        }

        // ReSharper disable once InconsistentNaming
        public static string GetN1QLBaseUriAsString(INodeAdapter adapter, BucketConfiguration config)
        {
            var uriAsString = string.Format(N1QLUriFormat,
                config.UseSsl ? Https : Http,
                adapter.Hostname,
                config.UseSsl ? adapter.N1QLSsl : adapter.N1QL);

            return uriAsString;
        }

        public static string GetBaseUriAsString(INodeAdapter adapter, BucketConfiguration config)
        {
            var uriAsString = string.Format(BaseUriFormat,
                config.UseSsl ? Https : Http,
                adapter.Hostname,
                config.UseSsl ? adapter.MgmtApiSsl: adapter.MgmtApi);

            return uriAsString;
        }

        public static string GetAnalyticsUri(INodeAdapter adapter, BucketConfiguration config)
        {
            var uri = string.Format(AnalyticsUriFormat,
                config.UseSsl ? Https : Http,
                adapter.Hostname,
                config.UseSsl ? adapter.AnalyticsSsl : adapter.Analytics);

            return uri;
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
