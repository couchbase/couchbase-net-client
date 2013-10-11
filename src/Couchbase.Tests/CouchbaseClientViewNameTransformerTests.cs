using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using Couchbase.Configuration;
using Couchbase.Tests.Mocks;
using NUnit.Framework;

namespace Couchbase.Tests
{
	[TestFixture]
	public class CouchbaseClientViewNameTransformerTests
	{
        /// <summary>
        /// @test: Design document name is prefixed with "dev_" in development mode
        /// @pre: Default configuration to initialize client in app.config, create a design document in server named "foo"
        /// @post: Test passes if name changes and is prefixed with dev_ as expected
        /// </summary>
		[Test]
		public void When_Setting_Design_Document_Name_Transformer_To_Dev_Views_Are_Prefixed_With_Dev()
		{
            TestTransformedDesignDocName(new DevelopmentModeNameTransformer(), "foo", "dev_foo");
		}

        /// <summary>
        /// @test: Design document name is not prefixed in production views
        /// @pre: Default configuration to initialize client in app.config, create a design document in server named "foo"
        /// @post: Test passes if name does not change and is not prefixed with any string
        /// </summary>
		[Test]
		public void When_Setting_Design_Document_Name_Transformer_To_Prod_Views_Are_Not_Prefixed()
		{
            TestTransformedDesignDocName(new ProductionModeNameTransformer(), "foo", "foo");
		}

		private void TestTransformedDesignDocName(INameTransformer transformer, string designDoc, string expected)
		{
			var clientWithConfig = GetClientWithConfig(transformer);
			var view = clientWithConfig.Item1.GetView(designDoc, "by_bar");
			foreach (var item in view) { }
			var request = GetHttpRequest(clientWithConfig);

			Assert.That(request.Path, Is.StringStarting(expected), "Path did not contain " + expected);
		}

        protected Tuple<CouchbaseClient, CouchbaseClientConfiguration> GetClientWithConfig(INameTransformer nameTransformer = null)
        {
            var config = new CouchbaseClientConfiguration();
            config.Urls.Add(new Uri(ConfigurationManager.AppSettings["CouchbaseServerUrl"] + "/pools"));
            config.Bucket = ConfigurationManager.AppSettings["DefaultBucketName"];
            config.DesignDocumentNameTransformer = nameTransformer ?? new DevelopmentModeNameTransformer();
            config.HttpClientFactory = new MockHttpClientFactory();

            var client = new CouchbaseClient(config);
            return Tuple.Create(client, config);
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
