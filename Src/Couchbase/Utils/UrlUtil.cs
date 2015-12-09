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

        public static Uri GetViewBaseUri(INodeAdapter adapter, BucketConfiguration config)
        {
            return new Uri(GetViewBaseUriAsString(adapter, config));
        }

        public static FailureCountingUri GetFailureCountingBaseUri(INodeAdapter adapter, BucketConfiguration config)
        {
            return new FailureCountingUri(GetN1QLBaseUriAsString(adapter, config));
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
    }
}
