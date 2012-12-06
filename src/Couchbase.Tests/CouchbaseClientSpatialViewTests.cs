using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Couchbase.Extensions;
using Enyim.Caching.Memcached;
using Couchbase.Operations;

namespace Couchbase.Tests
{
	[TestFixture]
	public class CouchbaseClientSpatialViewTests : CouchbaseClientViewTestsBase
	{
        /// <summary>
        /// @test: Get spatial view of a given design name and view name, 
        /// all the propeties of view should have correct values
        /// @pre: Default configuration to initialize client in app.config, configure 
        /// City views in cluster
        /// @post: Test passes if view prperties are not null
        /// </summary>
		[Test]
		public void When_Querying_Spatial_View_Results_Are_Returned()
		{
			var view = _Client.GetSpatialView("cities", "by_location");
			foreach (var item in view)
			{
				Assert.That(item.Id, Is.Not.Null, "Item Id was null");
				Assert.That(item.BoundingBox, Is.Not.Null, "Bounding box was null");
				Assert.That(item.BoundingBox.Length, Is.GreaterThan(0), "Bounding length box was empty");
				Assert.That(item.Geometry.Type, Is.StringMatching("Point"), "Type was not place");
				Assert.That(item.Geometry.Coordinates, Is.InstanceOf<float[]>(), "Coordinates were missing");
			}

			Assert.That(view.Count(), Is.GreaterThan(0), "View count was 0");
		}

        /// <summary>
        /// @test: Get spatial view of generic type City of a given design name and view name, 
        /// set look up by id to true, all the propeties of view should have correct values
        /// @pre: Default configuration to initialize client in app.config, configure 
        /// City views in cluster
        /// @post: Test passes if view prperties are not null
        /// </summary>
		[Test]
		public void When_Querying_Spatial_View_With_Generics_And_Should_Lookup_Doc_By_Id_Is_True_Results_Are_Returned()
		{
			var view = _Client.GetSpatialView<City>("cities", "by_location", true);
			foreach (var item in view)
			{
				Assert.That(item.Id, Is.Not.Null, "Id was null");
				Assert.That(item.Name, Is.Not.Null, "Name was null");
				Assert.That(item.State, Is.Not.Null, "State was null");
				Assert.That(item.Type, Is.StringMatching("city"), "Type was not city");
			}

			Assert.That(view.Count(), Is.GreaterThan(0), "View count was 0");
		}

        /// <summary>
        /// @test: Get spatial view of generic type City of a given design name and view name, 
        /// set look up by id to false, all the propeties of view should have correct values
        /// @pre: Default configuration to initialize client in app.config, configure 
        /// City views in cluster
        /// @post: Test passes if the count of view is greater than 0
        /// </summary>
		[Test]
		public void When_Querying_Spatial_View_With_Generics_And_Should_Lookup_Doc_By_Id_Is_False_Results_Are_Returned()
		{
			var view = _Client.GetSpatialView<CityProjection>("cities", "by_location_with_city_name", false);
			foreach (var item in view)
			{
				Assert.That(item.CityState, Is.Not.Null);
			}

			Assert.That(view.Count(), Is.GreaterThan(0), "View count was 0");
		}

        /// <summary>
        /// @test: Get spatial view of generic type City of a given design name and view name, 
        /// set limit to 2, then the view should return 2 rows
        /// @pre: Default configuration to initialize client in app.config, configure 
        /// City views in cluster
        /// @post: Test passes if view count is equal to 2
        /// </summary>
		[Test]
		public void When_Querying_Spatial_View_With_Limit_Rows_Are_Limited()
		{
			var view = _Client.GetSpatialView("cities", "by_location").Limit(2);
			Assert.That(view.Count(), Is.EqualTo(2), "View count was not 2");
		}

        /// <summary>
        /// @test: Get spatial view with bounding box, the result rows would be limited but 
        /// should have at least one record
        /// @pre: Default configuration to initialize client in app.config, configure 
        /// City views in cluster
        /// @post: Test passes if view count is at least one
        /// </summary>
		[Test]
		public void When_Querying_Spatial_View_With_Bounding_Box_Rows_Are_Limited()
		{
			var hasAtLeastOneRecord = false;
			var view = _Client.GetSpatialView("cities", "by_location").BoundingBox(-73.789673f, 41.093704f, -71.592407f, 42.079742f); //bbox around Connecticut
			foreach (var item in view)
			{
				hasAtLeastOneRecord = true;
				var doc = _Client.GetJson<City>(item.Id);
				Assert.That(doc.State, Is.StringMatching("CT"), "State was " + doc.State + " not CT");
			}

			Assert.That(hasAtLeastOneRecord, Is.True, "No records found");
		}

        /// <summary>
        /// @test: Store a new json format document and then get spatial view with stale mode false,
        /// the view result will contain new document
        /// @pre: Default configuration to initialize client in app.config, create a new json file for view
        /// @post: Test passes if view contains new json document
        /// </summary>
		[Test]
		public void When_Iterating_Over_A_Non_Stale_Spatial_View_Deleted_Keys_Return_Null()
		{
			var json = "{ \"name\" : \"New Britain\", \"state\" : \"CT\", \"type\" : \"city\", \"loc\" : [-72.4714, 41.4030] }";
			var key = "city_CT_New_Britain";

			var storeResult = _Client.ExecuteStore(StoreMode.Set, key, json, PersistTo.One);
			StoreAssertPass(storeResult);

			//force view to have new doc indexed
			var view = _Client.GetSpatialView<City>("cities", "by_location", true).Stale(StaleMode.False);

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

			view = _Client.GetSpatialView<City>("cities", "by_location", true).Stale(StaleMode.AllowStale);
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