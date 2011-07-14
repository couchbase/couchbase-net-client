using System;
using System.Linq;
using Membase;
using Membase.Configuration;
using Couchbase.Configuration;
using System.Diagnostics;
using System.Collections.Generic;
using System.Configuration;

namespace Couchbase
{
	/// <summary>
	/// Couchbase client.
	/// </summary>
	public class CouchbaseClient : MembaseClient
	{
		private static readonly ICouchbaseClientConfiguration DefaultConfig = (ICouchbaseClientConfiguration)ConfigurationManager.GetSection("couchbase");

		private INameTransformer documentNameTransformer;
		internal readonly CouchbasePool PoolInstance;

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Couchbase.CouchbaseClient" /> class using the default configuration and bucket.
		/// </summary>
		/// <remarks>The configuration is taken from the /configuration/couchbase section.</remarks>
		public CouchbaseClient() : this(DefaultConfig) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Couchbase.CouchbaseClient" /> class using the default configuration and the specified bucket.
		/// </summary>
		/// <remarks>The configuration is taken from the /configuration/couchbase section.</remarks>
		public CouchbaseClient(string bucketName, string bucketPassword) :
			this(DefaultConfig, bucketName, bucketPassword) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Couchbase.CouchbaseClient" /> class using a custom configuration provider.
		/// </summary>
		/// <param name="configuration">The custom configuration provider.</param>
		public CouchbaseClient(ICouchbaseClientConfiguration configuration) :
			this(configuration, configuration.Bucket, configuration.BucketPassword) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Couchbase.CouchbaseClient" /> class using the specified configuration 
		/// section and the specified bucket.
		/// </summary>
		/// <param name="sectionName">The name of the configuration section to load.</param>
		/// <param name="bucketName">The name of the bucket this client will connect to.</param>
		/// <param name="bucketPassword">The password of the bucket this client will connect to.</param>
		public CouchbaseClient(string sectionName, string bucketName, string bucketPassword) :
			this((ICouchbaseClientConfiguration)ConfigurationManager.GetSection(sectionName), bucketName, bucketPassword) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Couchbase.CouchbaseClient" /> class 
		/// using a custom configuration provider and the specified bucket name and password.
		/// </summary>
		/// <param name="configuration">The custom configuration provider.</param>
		/// <param name="bucketName">The name of the bucket this client will connect to.</param>
		/// <param name="bucketPassword">The password of the bucket this client will connect to.</param>
		public CouchbaseClient(ICouchbaseClientConfiguration configuration, string bucketName, string bucketPassword) :
			this(new CouchbasePool(configuration, bucketName, bucketPassword), configuration) { }

		protected CouchbaseClient(CouchbasePool pool, ICouchbaseClientConfiguration configuration)
			: base(pool, configuration)
		{
			this.PoolInstance = (CouchbasePool)this.Pool;

			this.documentNameTransformer = configuration.CreateDesignDocumentNameTransformer();
		}

		/// <summary>
		/// Returns an object representing the specified view in the specified design document.
		/// </summary>
		/// <param name="designName">The name of the design document.</param>
		/// <param name="viewName">The name of the view.</param>
		/// <returns></returns>
		public ICouchbaseView GetView(string designName, string viewName)
		{
			if (String.IsNullOrEmpty(designName)) throw new ArgumentNullException("designName");
			if (String.IsNullOrEmpty(viewName)) throw new ArgumentNullException("viewName");

			if (this.documentNameTransformer != null)
				designName = this.documentNameTransformer.Transform(designName);

			return new CouchbaseView(this, designName, viewName);
		}

		public IDictionary<string, object> Get(ICouchbaseView view)
		{
			var keys = view.Select(row => row.ItemId);

			return this.Get(keys);
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
