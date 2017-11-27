﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using Couchbase.Authentication;
using Couchbase.Configuration.Client;
using Couchbase.Core;

#if NET45
using System.Configuration;
using Couchbase.Configuration.Client.Providers;
#endif

namespace Couchbase
{
    /// <summary>
    /// A helper object for working with a <see cref="Cluster"/> instance.
    /// </summary>
    /// <remarks>Creates a singleton instance of a <see cref="Cluster"/> object.</remarks>
    /// <remarks>Call <see cref="Initialize()"/> before calling <see cref="Get()"/> to get the instance.</remarks>
    public class ClusterHelper
    {
        private static Lazy<Cluster> _instance;
        private static readonly object _syncObject = new object();
        private static readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);

        private static readonly Dictionary<string, IBucket> _buckets = new Dictionary<string, IBucket>();

        /// <summary>
        /// True if the <see cref="ClusterHelper"/> has been initialized.  Calling
        /// <see cref="Close()"/> will reset this value to false.
        /// </summary>
        public static bool Initialized
        {
            get { return _instance != null; }
        }

        /// <summary>
        /// Returns a Singleton instance of the Cluster class.
        /// </summary>
        /// <remarks>
        /// Call one of the Initialize() overloads to create or recreate the Singleton instance.
        /// However, Initialize() should only be called when the process starts up.
        /// </remarks>
        /// <returns>A Singleton instance of the Cluster class.</returns>
        /// <exception cref="Couchbase.Core.InitializationException">Thrown if Initialize is not called before accessing this method.</exception>
        public static Cluster Get()
        {
            if (_instance == null)
            {
                throw new InitializationException("Call Cluster.Initialize() before calling this method.");
            }
            return _instance.Value;
        }

        /// <summary>
        /// Opens or gets an <see cref="IBucket"/> instance from the <see cref="ICluster"/> that this <see cref="ClusterHelper"/> is wrapping.
        /// The <see cref="IBucket"/> will be cached and subsequent requests for a <see cref="IBucket"/> of the same name will return the
        /// cached instance.
        /// </summary>
        /// <param name="bucketName">The name of the <see cref="IBucket"/> to open or get.</param>
        /// <returns>An <see cref="IBucket"/>instance</returns>
        /// <remarks>Before calling you must call <see cref="ClusterHelper.Initialize()"/>.</remarks>
        public static IBucket GetBucket(string bucketName)
        {
            return GetBucket(bucketName, null);
        }

        /// <summary>
        /// Opens or gets an <see cref="IBucket"/> instance from the <see cref="ICluster"/> that this <see cref="ClusterHelper"/> is wrapping.
        /// The <see cref="IBucket"/> will be cached and subsequent requests for a <see cref="IBucket"/> of the same name will return the
        /// cached instance.
        /// </summary>
        /// <param name="bucketName">The name of the <see cref="IBucket"/> to open or get.</param>
        /// <param name="password">Bucket password, or null for unsecured buckets.</param>
        /// <returns>An <see cref="IBucket"/>instance</returns>
        /// <remarks>Before calling you must call <see cref="ClusterHelper.Initialize()"/>.</remarks>
        public static IBucket GetBucket(string bucketName, string password)
        {
            if (_buckets.TryGetValue(bucketName, out IBucket bucket))
            {
                return bucket;
            }

            if (_instance == null)
            {
                throw new InitializationException("Call Cluster.Initialize() before calling this method.");
            }

            _semaphoreSlim.Wait();
            try
            {
                if (_buckets.TryGetValue(bucketName, out bucket))
                {
                    return bucket;
                }

                var cluster = _instance.Value;
                var bucketPassword = DeterminePassword(cluster, bucketName, password);
                bucket = cluster.OpenBucket(bucketName, bucketPassword);
                _buckets.Add(bucketName, bucket);
                return bucket;
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        /// <summary>
        /// Opens or gets an <see cref="IBucket"/> instance from the <see cref="ICluster"/> that this <see cref="ClusterHelper"/> is wrapping.
        /// The <see cref="IBucket"/> will be cached and subsequent requests for a <see cref="IBucket"/> of the same name will return the
        /// cached instance.
        /// </summary>
        /// <param name="bucketName">The name of the <see cref="IBucket"/> to open or get.</param>
        /// <returns>An <see cref="IBucket"/>instance</returns>
        /// <remarks>Before calling you must call <see cref="ClusterHelper.Initialize()"/>.</remarks>
        public static async Task<IBucket> GetBucketAsync(string bucketName)
        {
            return await GetBucketAsync(bucketName, null);
        }

        /// <summary>
        /// Opens or gets an <see cref="IBucket"/> instance from the <see cref="ICluster"/> that this <see cref="ClusterHelper"/> is wrapping.
        /// The <see cref="IBucket"/> will be cached and subsequent requests for a <see cref="IBucket"/> of the same name will return the
        /// cached instance.
        /// </summary>
        /// <param name="bucketName">The name of the <see cref="IBucket"/> to open or get.</param>
        /// <param name="password">Bucket password, or null for unsecured buckets.</param>
        /// <returns>An <see cref="IBucket"/>instance</returns>
        /// <remarks>Before calling you must call <see cref="ClusterHelper.Initialize()"/>.</remarks>
        public static async Task<IBucket> GetBucketAsync(string bucketName, string password)
        {
            if (_buckets.TryGetValue(bucketName, out IBucket bucket))
            {
                return bucket;
            }

            if (_instance == null)
            {
                throw new InitializationException("Call Cluster.Initialize() before calling this method.");
            }

            await _semaphoreSlim.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_buckets.TryGetValue(bucketName, out bucket))
                {
                    return bucket;
                }

                var cluster = _instance.Value;
                var bucketPassword = DeterminePassword(cluster, bucketName, password);
                bucket = await cluster.OpenBucketAsync(bucketName, bucketPassword);
                _buckets.Add(bucketName, bucket);
                return bucket;
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        private static string DeterminePassword(ICluster cluster, string name, string password)
        {
            if (!string.IsNullOrWhiteSpace(password))
            {
                return password;
            }

            // try to find a password in configuration
            if (cluster.Configuration.BucketConfigs.TryGetValue(name, out BucketConfiguration bucketConfig)
                && bucketConfig.Password != null)
            {
                return bucketConfig.Password;
            }

            return null;
        }

        public static void RemoveBucket(string bucketName)
        {
            _semaphoreSlim.Wait();
            try
            {
                if (!_buckets.TryGetValue(bucketName, out IBucket bucket))
                {
                    return;
                }

                _buckets.Remove(bucketName);
                bucket.Dispose();
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        /// <summary>
        /// Initializes the Cluster instance using a given factory Func.
        /// </summary>
        /// <remarks>
        /// Call this on the Cluster object before calling Get() to return an instance. Note that
        /// this method should only be called during application or process startup or in certain
        /// scenarios where you explicitly want to reinitialize the current cluster instance.
        /// </remarks>
        /// <param name="factory">The factory Func that creates the instance.</param>
        private static void Initialize(Func<Cluster> factory)
        {
            lock (_syncObject)
            {
                if (_instance != null && _instance.IsValueCreated)
                {
                    var cluster = _instance.Value;
                    if (cluster != null)
                    {
                        cluster.Dispose();
                        _instance = null;
                    }
                }
                _instance = new Lazy<Cluster>(factory);
            }
        }

        /// <summary>
        /// Initializes a new Cluster instance with a given ClientConfiguration and ClusterManager.
        /// This overload is primarily provided for testing given that it allows you to set the
        /// major dependencies of the Cluster class and it's scope is internal.
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="clusterManager"></param>
        internal static void Initialize(ClientConfiguration configuration, IClusterController clusterManager)
        {
            if (configuration == null || clusterManager == null)
            {
                throw new ArgumentNullException(configuration == null ? "configuration" : "clusterManager");
            }

            configuration.Initialize();
            var factory = new Func<Cluster>(() => new Cluster(configuration, clusterManager));
            Initialize(factory);
        }

        /// <summary>
        /// Creates a Cluster instance.
        /// </summary>
        /// <param name="configuration">
        /// The ClientConfiguration to use when initialize the internal ClusterManager
        /// </param>
        /// <remarks>
        /// This is an heavy-weight object intended to be long-lived. Create one per process or App.Domain.
        /// </remarks>
        public static void Initialize(ClientConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException("configuration");
            }

            configuration.Initialize();
            var factory = new Func<Cluster>(() => new Cluster(configuration));
            Initialize(factory);
        }

        /// <summary>
        /// Creates a Cluster instance.
        /// </summary>
        /// <param name="configuration">
        /// The ClientConfiguration to use when initialize the internal ClusterManager
        /// </param>
        /// <param name="authenticator">The <see cref="IAuthenticator"/> for auethenticating against the cluster.
        /// Use a <see cref="PasswordAuthenticator"/> for RBAC in Couchbase Server 5.0 and greater.</param>
        /// <remarks>
        /// This is an heavy-weight object intended to be long-lived. Create one per process or App.Domain.
        /// </remarks>
        public static void Initialize(ClientConfiguration configuration, IAuthenticator authenticator)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException("configuration");
            }
            if (authenticator == null)
            {
                throw new ArgumentNullException("authenticator");
            }

            configuration.Initialize();
            var factory = new Func<Cluster>(() =>
            {
                var cluster = new Cluster(configuration);
                cluster.Authenticate(authenticator);
                return cluster;
            });
            Initialize(factory);
        }

        /// <summary>
        /// Creates a Cluster instance using the default configuration. This is overload is suitable
        /// for development only as it will use localhost (127.0.0.1) and the default Couchbase REST
        /// and Memcached ports.
        /// <see cref="http://docs.couchbase.com/couchbase-manual-2.5/cb-install/#network-ports" />
        /// </summary>
        public static void Initialize()
        {
            var factory = new Func<Cluster>(() => new Cluster());
            Initialize(factory);
        }

        /// <summary>
        /// Ctor for creating Cluster instance.
        /// </summary>
        /// <param name="definition">The configuration definition loaded from a configuration file.</param>
        public static void Initialize(ICouchbaseClientDefinition definition)
        {
            var configuration = new ClientConfiguration(definition);
            configuration.Initialize();

            var factory = new Func<Cluster>(() => new Cluster(configuration));
            Initialize(factory);
        }

#if NET45

        /// <summary>
        /// Ctor for creating Cluster instance.
        /// </summary>
        /// <param name="configurationSectionName">The name of the configuration section to use.</param>
        /// <remarks>Note that <see cref="CouchbaseClientSection"/> needs include the sectionGroup name as well: "couchbaseSection/couchbase" </remarks>
        public static void Initialize(string configurationSectionName)
        {
            var configurationSection =
                (CouchbaseClientSection)ConfigurationManager.GetSection(configurationSectionName);
            var configuration = new ClientConfiguration(configurationSection);
            configuration.Initialize();

            var factory = new Func<Cluster>(() => new Cluster(configuration));
            Initialize(factory);
        }

#endif

        /// <summary>
        /// Returns the number of <see cref="IBucket"/> instances internally cached by the <see cref="ClusterHelper"/>.
        /// </summary>
        /// <returns></returns>
        public static int Count()
        {
            return _buckets.Count;
        }

        /// <summary>
        /// Disposes the current <see cref="Cluster"/> instance and cleans up resources.
        /// </summary>
        public static void Close()
        {
            lock (_syncObject)
            {
                if (_instance == null || !_instance.IsValueCreated)
                {
                    _instance = null;
                    return;
                }
                foreach (var bucket in _buckets.Values)
                {
                    bucket.Dispose();
                }
                _buckets.Clear();
                var cluster = _instance.Value;
                if (cluster == null) return;
                cluster.Dispose();
                _instance = null;
            }
        }
    }
}

#region [ License information ]

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