using System;
using System.Linq;

namespace Couchbase.Configuration.Server.Serialization
{
    internal static class BootstrapExtensions
    {
        public static Uri GetPoolsUri(this Bootstrap bootstrap, Uri baseUri)
        {
            if (!bootstrap.Pools.Any())
            {
                const string msg = "No servers returned by bootstrap url. This may indicate that you are attempting to bootstrap to a server that has not joined a cluster yet.";
                throw new BootstrapException(msg);
            }
            return FixupUri(baseUri, bootstrap.Pools.First().Uri);
        }

        public static Uri GetServersGroupUri(this Pools pools, Uri baseUri)
        {
            return FixupUri(baseUri, pools.ServerGroupsUri);
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

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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