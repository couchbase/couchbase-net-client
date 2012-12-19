using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using Couchbase.Configuration;
using Couchbase.Tests.Mocks;
using System.IO;
using System.Net;
using Enyim.Caching.Memcached;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Couchbase.Tests
{
	public abstract class CouchbaseClientViewTestsBase : CouchbaseClientTestsBase
	{
		[SetUp]
		public void Setup()
		{
			CreateDocsFromFile("Data\\CityDocs.json", "city_", "state", "name");
			CreateViewFromFile("Data\\CityViews.json", "cities");
		}

		protected Tuple<CouchbaseClient, CouchbaseClientConfiguration> GetClientWithConfig(INameTransformer nameTransformer = null)
		{
			var config = new CouchbaseClientConfiguration();
            config.Urls.Add(new Uri(ConfigurationManager.AppSettings["CouchbaseServerUrl"] + "/pools"));
		    config.Bucket = ConfigurationManager.AppSettings["CouchbaseServerUrl"];
			config.DesignDocumentNameTransformer = nameTransformer ?? new DevelopmentModeNameTransformer();
			config.HttpClientFactory = new MockHttpClientFactory();

			return Tuple.Create(new CouchbaseClient(config), config);
		}

		protected MockHttpRequest GetHttpRequest(Tuple<CouchbaseClient, CouchbaseClientConfiguration> clientWithConfig)
		{
			var httpClientFactory = clientWithConfig.Item2.HttpClientFactory as MockHttpClientFactory;
			var httpClient = httpClientFactory.Client as MockHttpClient;
			return httpClient.Request as MockHttpRequest;
		}

		protected void CreateViewFromFile(string viewFile, string docName)
		{
			var viewContent = File.ReadAllText(viewFile).Replace("[[DESIGNDOC]]", docName);
			byte[] arr = System.Text.Encoding.UTF8.GetBytes(viewContent);
            var request = (HttpWebRequest)HttpWebRequest.Create(ConfigurationManager.AppSettings["CouchbaseServerUrl"] + "/couchBase/default/_design/" + docName);
			request.Method = "PUT";
			request.ContentType = "application/json";
            request.ContentLength = arr.Length;
			var dataStream = request.GetRequestStream();
			dataStream.Write(arr, 0, arr.Length);
			dataStream.Close();
			var response = (HttpWebResponse)request.GetResponse();
			string returnString = response.StatusCode.ToString();
		}

		protected void CreateDocsFromFile(string docFile, string keyPrefix, params string[] keyProperties)
		{
			using (var reader = new StreamReader(docFile))
			{
				string line;
				while ((line = reader.ReadLine()) != null)
				{
					var lineObj = JsonConvert.DeserializeObject(line) as JObject;

					var keys = "";
					foreach (var item in keyProperties)
					{
						keys += lineObj[item].ToString().Replace(" ", "_") + "_";
					}

					var key = keyPrefix + keys.TrimEnd('_');

					var result = _Client.ExecuteStore(StoreMode.Set, key, line);
					Assert.That(result.Success, Is.True, string.Format("Store failed for {0} with message {1}", key, result.Message));
				}
			}
		}

		protected class City
		{
			public string Id { get; set; }

			public string Name { get; set; }

			public string State { get; set; }

			public string Type { get; set; }
		}

		protected class CityProjection
		{
			public string CityState { get; set; }
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