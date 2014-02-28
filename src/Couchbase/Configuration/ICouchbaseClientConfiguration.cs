using System;
using System.Collections.Generic;
using System.Net;
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached;

namespace Couchbase.Configuration
{
	public interface ICouchbaseClientConfiguration
	{
		/// <summary>
		/// Gets the name of the bucket to be used. If not specified the "default" bucket will be used.
		/// </summary>
		string Bucket { get; }

		/// <summary>
		/// Gets the password used to connect to the bucket.
		/// </summary>
		/// <remarks> If null, the bucket name will be used. Set to String.Empty to use an empty password.</remarks>
		string BucketPassword { get; }

		/// <summary>
		/// Gets the name of the administrator account for the cluster
		/// </summary>
		string Username { get; }

		/// <summary>
		/// Gets the password of the administrator account for the cluster
		/// </summary>
		string Password { get; }

		/// <summary>
		/// Gets a list of <see cref="T:IPEndPoint"/> each representing a Memcached server in the pool.
		/// </summary>
		IList<Uri> Urls { get; }

		/// <summary>
		/// Gets the configuration of the socket pool.
		/// </summary>
		ISocketPoolConfiguration SocketPool { get; }

		/// <summary>
		/// Gets the configuration of the heartbeat monitor.
		/// </summary>
		IHeartbeatMonitorConfiguration HeartbeatMonitor { get; }

		/// <summary>
		/// Gets the configuration of the http client.
		/// </summary>
		IHttpClientConfiguration HttpClient { get; }

		/// <summary>
		/// Creates an <see cref="T:Enyim.Caching.Memcached.IMemcachedKeyTransformer"/> instance which will be used to convert item keys for Memcached.
		/// </summary>
		IMemcachedKeyTransformer CreateKeyTransformer();

		/// <summary>
		/// Creates an <see cref="T:Enyim.Caching.Memcached.IMemcachedNodeLocator"/> instance which will be used to assign items to Memcached nodes.
		/// </summary>
		IMemcachedNodeLocator CreateNodeLocator();

		/// <summary>
		/// Creates an <see cref="T:Enyim.Caching.Memcached.ITranscoder"/> instance which will be used to serialize or deserialize items.
		/// </summary>
		ITranscoder CreateTranscoder();

		/// <summary>
		/// Creates an <see cref="T:Enyim.Caching.Memcached.IPerformanceMonitor"/> instance which will be used to monitor the performance of the client.
		/// </summary>
		IPerformanceMonitor CreatePerformanceMonitor();

        /// <summary>
        /// Creates a name transformer instance whihc will be used to change the design document's name before retrieving it from the server.
        /// </summary>
        /// <remarks>
        /// This way you can create additional views over the same data, and use one set production and other set(s) for testing and development without changing your application code. (Couchbase provides UI support 'development views' where the name of the design document is prefixed with dev_.)
        /// </remarks>
        /// <returns>A transformed name.</returns>
        INameTransformer CreateDesignDocumentNameTransformer();
        IHttpClient CreateHttpClient(Uri baseUri);

		TimeSpan RetryTimeout { get; }
		TimeSpan ObserveTimeout { get; }
		int RetryCount { get; }

        /// <summary>
        /// The number of loops to try over a set of nodes when cluster has changed
        /// (i.e. rebalance, failover, etc) and NotMyVBucket response is returned from
        /// the cluster. Most of the times this is resolved in the first iteration.
        /// </summary>
        int VBucketRetryCount { get; }

		/// <summary>
		/// Timeout used for timing out HTTP requests to RESTful API calls
		/// </summary>
		TimeSpan HttpRequestTimeout { get; }

	    int ViewRetryCount { get; set; }
	}
}

#region [ License information          ]
/* ************************************************************
 * 
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2012 Couchbase, Inc.
 *    @copyright 2010 Attila Kiskó, enyim.com
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
