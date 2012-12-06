using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.IO;
using Enyim.Caching.Memcached;
using System.Net;
using Newtonsoft.Json;
using Couchbase.Operations;

namespace Couchbase.Tests
{
	[TestFixture]
	public class CouchbaseClientGenericViewTests : CouchbaseClientViewTestsBase
	{
        /// <summary>
        /// @test: Set shouldlookupDocById to true, GetView() method will retrieve
        /// the view by id and verifies that all properties of 
        /// view type (City in this context) are correct
        /// @pre: Define CityViews.json to create views
        /// @post: Test passes if GetView returns results correctly
        /// </summary>
		[Test]
		public void When_Should_Lookup_By_Id_Is_True_Document_Is_Retrieved_By_Id()
		{
			var view = _Client.GetView<City>("cities", "by_name", true).Stale(StaleMode.False);
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

        /// <summary>
        /// @test: Set shouldlookupDocById to false, GetView() method will retrieve
        /// the view by id and verifies that all properties of 
        /// view type (City in this context) are correct
        /// @pre: Define CityViews.json to create views
        /// @post: Test passes if GetView returns results correctly
        /// </summary>
		[Test]
		public void When_Should_Lookup_By_Id_Is_False_Document_Is_Deserialized_By_Property_Mapping()
		{
			var view = _Client.GetView<CityProjection>("cities", "by_city_and_state", false).Stale(StaleMode.False);
			foreach (var item in view)
			{
				Assert.That(item.CityState, Is.Not.Null, "CityState was null");
			}

			Assert.That(view.Count(), Is.GreaterThan(0), "View count was 0");
		}

        /// <summary>
        /// @test: Store a json format document and then get the view with stale mode false
        /// @pre: Define data to be stored in json format
        /// @post: Test passes if GetView returns results correctly
        /// </summary>
		[Test]
		public void When_Iterating_Over_A_Non_Stale_View_Deleted_Keys_Return_Null()
		{
			var json = "{ \"name\" : \"New Britain\", \"state\" : \"CT\", \"type\" : \"city\", \"loc\" : [-72.4714, 41.4030] }";
			var key = "city_CT_New_Britain";

			var storeResult = _Client.ExecuteStore(StoreMode.Set, key, json, PersistTo.One);
			StoreAssertPass(storeResult);

			//force view to have new doc indexed
			var view = _Client.GetView<City>("cities", "by_name", true).Stale(StaleMode.False);

			var viewContainsNewDoc = false;
			foreach (var item in view)
			{
				if (item.Id == key)
				{
					viewContainsNewDoc = true;
					break;
				}
			}

			Assert.That(viewContainsNewDoc, Is.True, "View did not contain new doc");

			var removeResult = _Client.ExecuteRemove(key);
			Assert.That(removeResult.Success, Is.True, "Remove failed");

			var getResult = _Client.ExecuteGet(key);
			GetAssertFail(getResult);

			view = _Client.GetView<City>("cities", "by_name", true).Stale(StaleMode.AllowStale);
			var nullItemCount = 0;
			foreach (var item in view)
			{
				if (item == null) ++nullItemCount;
			}

			Assert.That(nullItemCount, Is.EqualTo(1), "Null item count was not 1");
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
