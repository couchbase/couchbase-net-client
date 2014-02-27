using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Configuration.Server.Serialization
{
    internal static class BootstrapExtensions
    {
        public static Uri GetPoolsUri(this Bootstrap bootstrap, Uri baseUri)
        {
            if (!bootstrap.Pools.Any())
            {
                throw new BootstrapException("No servers returned by boostrap url.");
            }
            return FixupUri(baseUri, bootstrap.Pools.First().Uri);
        }

        public static Uri GetBucketUri(this Pools pools, Uri baseUri)
        {
            return FixupUri(baseUri, pools.Buckets.Uri);
        }

        public static Uri GetStreamingBucketUri(this BucketConfig bucketConfig, Uri baseUri)
        {
            return FixupUri(baseUri, bucketConfig.StreamingUri);
        }

        static Uri FixupUri(Uri baseUri, string relativeUri)
        {
            var basePath = baseUri.AbsoluteUri.Replace("/pools", "");
            basePath = basePath.Replace("/pools/", "");
            return new Uri(String.Concat(basePath, relativeUri));
        }
    }
}
