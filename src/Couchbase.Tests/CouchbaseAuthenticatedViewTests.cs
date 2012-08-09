using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.Net;
using Couchbase.Configuration;

namespace Couchbase.Tests
{
	[TestFixture]
	public class CouchbaseAuthenticatedViewTests : CouchbaseClientViewTestsBase
	{

		[Test]
		public void When_Bucket_Is_Authenticated_View_Returns_Results()
		{
			var view = getClient("authenticated", "secret").GetView("cities", "by_name");
			foreach (var item in view) { }

			Assert.That(view.Count(), Is.EqualTo(1), "Row count was not 1");
		}

		[ExpectedException(typeof(WebException), ExpectedMessage="401", MatchType=MessageMatch.Contains)]
		[Test]
		public void When_Bucket_Is_Authenticated_And_No_Credentials_Are_Provided_Exception_Is_Thrown()
		{
			var view = getClient("authenticated", "").GetView("cities", "by_name");
			foreach (var item in view) { Console.WriteLine(item); }

		}

		private CouchbaseClient getClient(string username, string password)
		{
			var config = new CouchbaseClientConfiguration();
			config.Urls.Add(new Uri("http://localhost:8091/pools"));
			config.Bucket = username;
			config.BucketPassword = password;
			config.DesignDocumentNameTransformer = new DevelopmentModeNameTransformer();
			config.HttpClientFactory = new HammockHttpClientFactory();

			return new CouchbaseClient(config);
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
