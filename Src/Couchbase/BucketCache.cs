using System;
using System.Collections.Generic;
using System.Threading;
using Couchbase.Authentication;
using Couchbase.Configuration.Client;
using Couchbase.Core;

namespace Couchbase
{
    /// <summary>
    /// Provides access to <see cref="IBucket"/> instances.
    /// </summary>
    public class BucketCache : IBucketCache, IDisposable
    {
        private readonly SemaphoreSlim syncRoot = new SemaphoreSlim(1, 1);
        private readonly Dictionary<string, IBucket> buckets = new Dictionary<string, IBucket>();
        private readonly Lazy<ICluster> cluster;

        /// <summary>
        /// Initializes a new instance of the <see cref="BucketCache"/> class.
        /// </summary>
        /// <param name="configuration">The <see cref="ClientConfiguration"/> to use when initialize the internal <see cref="BucketCache"/>.</param>
        public BucketCache(ClientConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            cluster = new Lazy<ICluster>(() => new Cluster(configuration));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BucketCache"/> class.
        /// </summary>
        /// <param name="configuration">The <see cref="ClientConfiguration"/> to use when initialize the internal <see cref="BucketCache"/>.</param>
        /// <param name="authenticator">The <see cref="IAuthenticator"/> for authenticating against the cluster.
        /// Use a <see cref="PasswordAuthenticator"/> for RBAC in Couchbase Server 5.0 and greater.</param>
        public BucketCache(ClientConfiguration configuration, IAuthenticator authenticator)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (authenticator == null)
            {
                throw new ArgumentNullException(nameof(authenticator));
            }

            cluster = new Lazy<ICluster>(() =>
            {
                var cluster = new Cluster(configuration);
                cluster.Authenticate(authenticator);

                return cluster;
            });
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BucketCache"/> class.
        /// </summary>
        /// <param name="definition">The configuration definition loaded from a configuration file.</param>
        public BucketCache(ICouchbaseClientDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var configuration = new ClientConfiguration(definition);
            configuration.Initialize();

            cluster = new Lazy<ICluster>(() => new Cluster(configuration));
        }

        // for testing purposes
        internal BucketCache(Func<ICluster> clusterFactory)
        {
            cluster = new Lazy<ICluster>(clusterFactory);
        }

        /// <summary>
        /// Opens or gets an <see cref="IBucket"/> instance from the <see cref="ICluster"/> that this <see cref="BucketCache"/> is wrapping.
        /// The <see cref="IBucket"/> will be cached and subsequent requests for an <see cref="IBucket"/> of the same name will return the
        /// cached instance.
        /// </summary>
        /// <param name="bucketName">The name of the <see cref="IBucket"/> to open or get.</param>
        /// <returns>An <see cref="IBucket"/> instance.</returns>
        public IBucket Get(string bucketName)
        {
            return Get(bucketName, null);
        }

        /// <summary>
        /// Opens or gets an <see cref="IBucket"/> instance from the <see cref="ICluster"/> that this <see cref="BucketCache"/> is wrapping.
        /// The <see cref="IBucket"/> will be cached and subsequent requests for an <see cref="IBucket"/> of the same name will return the
        /// cached instance.
        /// </summary>
        /// <param name="bucketName">The name of the <see cref="IBucket"/> to open or get.</param>
        /// <param name="password">Bucket password, or null for unsecured buckets.</param>
        /// <returns>An <see cref="IBucket"/> instance.</returns>
        public IBucket Get(string bucketName, string password)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
            {
                if (bucketName == null)
                {
                    throw new ArgumentNullException(nameof(bucketName));
                }

                throw new ArgumentException("The parameter bucketName cannot be empty or whitespace.", nameof(bucketName));
            }

            if (!buckets.TryGetValue(bucketName, out var bucket))
            {
                syncRoot.Wait();

                try
                {
                    if (!buckets.ContainsKey(bucketName))
                    {
                        bucket = OpenBucket(bucketName, password);

                        buckets.Add(bucketName, bucket);
                    }
                }
                finally
                {
                    syncRoot.Release();
                }
            }

            return bucket;
        }

        /// <summary>
        /// Removes the given bucket from the cached buckets.
        /// </summary>
        /// <param name="bucketName">The name of the <see cref="IBucket"/> to remove.</param>
        public void Remove(string bucketName)
        {
            syncRoot.Wait();

            try
            {
                if (buckets.TryGetValue(bucketName, out var bucket))
                {
                    buckets.Remove(bucketName);

                    bucket.Dispose();
                }
            }
            finally
            {
                syncRoot.Release();
            }
        }

        /// <summary>
        /// Releases all resources used by the <see cref="BucketCache"/>.
        /// </summary>
        public void Dispose()
        {
            if (cluster.IsValueCreated)
            {
                foreach (var bucket in buckets.Values)
                {
                    bucket.Dispose();
                }

                buckets.Clear();

                cluster.Value.Dispose();
            }
        }

        private IBucket OpenBucket(string bucketName, string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                // try to find a password in configuration
                if (cluster.Value.Configuration.BucketConfigs.TryGetValue(bucketName, out var bucketConfig) && bucketConfig.Password != null)
                {
                    return cluster.Value.OpenBucket(bucketName, bucketConfig.Password);
                }

                return cluster.Value.OpenBucket(bucketName);
            }

            return cluster.Value.OpenBucket(bucketName, password);
        }
    }
}
