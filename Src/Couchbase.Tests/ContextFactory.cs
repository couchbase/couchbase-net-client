using Couchbase.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;

namespace Couchbase.Tests
{
    public static class ContextFactory
    {
        internal static MemcachedConfigContext GetMemcachedContext()
        {
            var bucketConfig = new BucketConfig();
            var clientConfig = new ClientConfiguration();

            return GetMemcachedContext(clientConfig, bucketConfig);
        }

        internal static MemcachedConfigContext GetMemcachedContext(IBucketConfig bucketConfig)
        {
            var clientConfig = new ClientConfiguration();

            return GetMemcachedContext(clientConfig, bucketConfig);
        }

        internal static MemcachedConfigContext GetMemcachedContext(ClientConfiguration clientConfig, IBucketConfig bucketConfig)
        {
            return new MemcachedConfigContext(bucketConfig, clientConfig, null, null, null, null, null, null);
        }

        internal static CouchbaseConfigContext GetCouchbaseContext()
        {
            var bucketConfig = new BucketConfig();
            var clientConfig = new ClientConfiguration();

            return GetCouchbaseContext(clientConfig, bucketConfig);
        }

        internal static CouchbaseConfigContext GetCouchbaseContext(ClientConfiguration clientConfig)
        {
            var bucketConfig = new BucketConfig();

            return GetCouchbaseContext(clientConfig, bucketConfig);
        }

        internal static CouchbaseConfigContext GetCouchbaseContext(IBucketConfig bucketConfig)
        {
            var clientConfig = new ClientConfiguration();

            return GetCouchbaseContext(clientConfig, bucketConfig);
        }

        internal static CouchbaseConfigContext GetCouchbaseContext(ClientConfiguration clientConfig, IBucketConfig bucketConfig)
        {
            return new CouchbaseConfigContext(bucketConfig, clientConfig, null, null, null, null, null, null);
        }
    }
}
