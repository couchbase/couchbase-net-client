using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Couchbase.Configuration;
using Couchbase.Tests.Mocks;

namespace Couchbase.Tests
{
	public abstract class CouchbaseClientViewTestsBase
	{
		protected Tuple<CouchbaseClient, CouchbaseClientConfiguration> GetClientWithConfig()
		{
			var config = new CouchbaseClientConfiguration();
			config.Urls.Add(new Uri("http://localhost:8091/pools/default"));
			config.Bucket = "default";
			config.DesignDocumentNameTransformer = new DevelopmentModeNameTransformer();
			config.HttpClientFactory = new MockHttpClientFactory();

			return Tuple.Create(new CouchbaseClient(config), config);
		}

		protected MockHttpRequest GetHttpRequest(Tuple<CouchbaseClient, CouchbaseClientConfiguration> clientWithConfig)
		{
			var httpClientFactory = clientWithConfig.Item2.HttpClientFactory as MockHttpClientFactory;
			var httpClient = httpClientFactory.Client as MockHttpClient;
			return httpClient.Request as MockHttpRequest;
		}
	}
}

#region [ License information          ]
/* ************************************************************
 * 
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2012 Couchbase, Inc.
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