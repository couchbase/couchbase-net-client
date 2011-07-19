using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Membase;
using Membase.Configuration;
using Enyim.Caching.Memcached;
using System.Net;
using Hammock;
using Couchbase.Configuration;
using System.Diagnostics;

namespace Couchbase
{
	public class CouchbasePool : MembasePool
	{
		private static readonly Enyim.Caching.ILog log = Enyim.Caching.LogManager.GetLogger(typeof(CouchbasePool));

		private INameTransformer docNameTransformer;
		private ICouchbaseClientConfiguration configuration;

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Membase.MembasePool" />.
		/// </summary>
		/// <param name="configuration">The configuration to be used.</param>
		public CouchbasePool(ICouchbaseClientConfiguration configuration) : this(configuration, configuration.Bucket, configuration.BucketPassword) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Membase.MembasePool" /> class using the specified configuration,
		/// bucket name and password.
		/// </summary>
		/// <param name="configuration">The configuration to be used.</param>
		/// <param name="bucketName">The name of the bucket to connect to. Overrides the configuration's Bucket property.</param>
		/// <param name="bucketPassword">The password to the bucket. Overrides the configuration's BucketPassword property.</param>
		public CouchbasePool(ICouchbaseClientConfiguration configuration, string bucketName, string bucketPassword)
			: base(configuration, bucketName, bucketPassword)
		{
			this.docNameTransformer = configuration.CreateDesignDocumentNameTransformer();
			this.configuration = configuration;
		}

		protected override IMemcachedNode CreateNode(IPEndPoint endpoint, ISaslAuthenticationProvider auth, Dictionary<string, object> nodeInfo)
		{
			string couchApiBase;
			if (!nodeInfo.TryGetValue("couchApiBase", out couchApiBase)
				|| String.IsNullOrEmpty(couchApiBase))
				throw new InvalidOperationException("The node configuration does not contain the required 'couchApiBase' attribute.");

			return new CouchbaseNode(endpoint, new Uri(couchApiBase), this.configuration, auth);
		}
	}
}

#region [ License information          ]
/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2011 Couchbase, Inc.
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
