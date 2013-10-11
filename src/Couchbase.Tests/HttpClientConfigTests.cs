using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Enyim.Caching;
using NUnit.Framework;
using Couchbase.Configuration;
using Enyim.Caching.Memcached;
using Couchbase.Tests.Utils;

namespace Couchbase.Tests
{
	[TestFixture]
	public class HttpClientConfigTests : CouchbaseClientViewTestsBase
	{
        private static readonly ILog Log = LogManager.GetLogger(typeof(HttpClientConfigTests));
        /// <summary>
        /// @test: Reads the configuration of Http client from App.config and gets the view in specified design document
        /// @pre: Add section named "httpclient-config-initconn" in App.config file,
        /// set the initializeConnection parameter to true
        /// configure all the parameters required to initialize Couchbase client like Uri, bucket, etc.
        /// @post: Test passes if successfully gets the view, fails otherwise
        /// </summary>
		[Test]
        public void View_Operations_Succeed_When_Initialize_Connection_Is_True()
		{
			var config = ConfigSectionUtils.GetConfigSection<CouchbaseClientSection>("httpclient-config-initconn");
            using (var client = new CouchbaseClient(config))
            {
                var view = client.GetView<City>("cities", "by_name", true).Stale(StaleMode.False);
                ViewPass(view);
            }
		}

        /// <summary>
        /// @test: Reads the configuration of Http client from App.config and gets the view in specified design document
        /// @pre: Add section named "httpclient-config-initconn" in App.config file,
        /// configure all the parameters required to initialize Couchbase client like Uri, bucket, etc.
        /// set the initializeConnection parameter to false
        /// @post: Test passes if successfully gets the view, fails otherwise
        /// </summary>
		[Test]
		public void View_Operations_Succeed_When_Initialize_Connection_Is_False()
		{
			var config = ConfigSectionUtils.GetConfigSection<CouchbaseClientSection>("httpclient-config-noinitconn");
            using (var client = new CouchbaseClient(config))
            {
                var view = client.GetView<City>("cities", "by_name", true).Stale(StaleMode.False);
                ViewPass(view);
            }
		}

        /// <summary>
        /// @test: when no configuration for Http client is mentioned in App.config,
        /// the test would get the view in specified design document
        /// @pre: no section in App.config file to initialize client
        /// @post: Test passes if successfully gets the view, fails otherwise
        /// </summary>
		[Test]
		public void View_Operations_Succeed_When_HTTPClient_Is_Not_Configured_In_App_Config()
		{
			var view = Client.GetView<City>("cities", "by_name", true).Stale(StaleMode.False);
			ViewPass(view);
		}

        /// <summary>
        /// Verifies all the properties of view and asserts true if it is not null, false otherwise
        /// </summary>
        /// <param name="view">Name of design view</param>
		private static void ViewPass(IView<City> view)
		{			
			foreach (var item in view)
			{
				Assert.That(item.Id, Is.Not.Null, "Item Id was null");
				Assert.That(item.Name, Is.Not.Null, "Item Name was null");
				Assert.That(item.State, Is.Not.Null, "Item State was null");
				Assert.That(item.Type, Is.EqualTo("city"), "Type was not city");
				Assert.That(item, Is.InstanceOf<City>(), "Item was not a City instance");
			}

			Assert.That(view.Count(), Is.GreaterThan(0), "View count was 0");
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
