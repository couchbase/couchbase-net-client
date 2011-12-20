using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Net;
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached;
using Membase.Configuration;
using Couchbase.Configuration;
using System.Diagnostics;

namespace Couchbase.Configuration
{
	/// <summary>
	/// Configures the <see cref="T:MembaseClient"/>.
	/// </summary>
	public class CouchbaseClientSection : MembaseClientSection, ICouchbaseClientConfiguration
	{
		/// <summary>
		/// Gets or sets the <see cref="T:Enyim.Caching.Memcached.IMemcachedNodeLocator"/> which will be used to assign items to Memcached nodes.
		/// </summary>
		[ConfigurationProperty("documentNameTransformer", IsRequired = false)]
		public ProviderElement<INameTransformer> DocumentNameTransformer
		{
			get { return (ProviderElement<INameTransformer>)base["documentNameTransformer"]; }
			set { base["documentNameTransformer"] = value; }
		}

		[ConfigurationProperty("httpClientFactory", IsRequired = false)]
		public ProviderElement<IHttpClientFactory> HttpClientFactory
		{
			get { return (ProviderElement<IHttpClientFactory>)base["httpClientFactory"]; }
			set { base["httpClientFactory"] = value; }
		}

		#region [ interface                     ]

		INameTransformer ICouchbaseClientConfiguration.CreateDesignDocumentNameTransformer()
		{
			return this.DocumentNameTransformer == null
					? null
					: this.DocumentNameTransformer.CreateInstance();
		}

		IHttpClientFactory clientFactory;

		IHttpClient ICouchbaseClientConfiguration.CreateHttpClient(Uri baseUri)
		{
			if (this.clientFactory == null)
			{
				var tmp = this.HttpClientFactory;

				this.clientFactory = tmp == null ? HammockHttpClientFactory.Instance : tmp.CreateInstance();
			}

			Debug.Assert(this.clientFactory != null);

			return this.clientFactory.Create(baseUri);
		}

		#endregion
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
