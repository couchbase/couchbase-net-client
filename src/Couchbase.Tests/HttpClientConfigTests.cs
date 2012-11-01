using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Couchbase.Configuration;
using Enyim.Caching.Memcached;
using Couchbase.Tests.Utils;

namespace Couchbase.Tests
{
	[TestFixture]
	public class HttpClientConfigTests : CouchbaseClientViewTestsBase
	{
		[Test]
		public void View_Operations_Succeed_When_Initialize_Connection_Is_True()
		{
			var config = ConfigSectionUtils.GetConfigSection<CouchbaseClientSection>("httpclient-config-initconn");
			var client = new CouchbaseClient(config);
			var view = client.GetView<City>("cities", "by_name", true).Stale(StaleMode.False);
			viewPass(view);
		}

		[Test]
		public void View_Operations_Succeed_When_Initialize_Connection_Is_False()
		{
			var config = ConfigSectionUtils.GetConfigSection<CouchbaseClientSection>("httpclient-config-noinitconn");
			var client = new CouchbaseClient(config);

			var view = client.GetView<City>("cities", "by_name", true).Stale(StaleMode.False);
			viewPass(view);			

		}

		[Test]
		public void View_Operations_Succeed_When_HTTP_Client_Is_Not_Configured_In_App_Config()
		{
			var view = _Client.GetView<City>("cities", "by_name", true).Stale(StaleMode.False);
			viewPass(view);	

		}

		private void viewPass(IView<City> view)
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
