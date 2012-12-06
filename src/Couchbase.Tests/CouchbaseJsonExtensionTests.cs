using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Newtonsoft.Json;
using Couchbase.Extensions;
using Enyim.Caching.Memcached;

namespace Couchbase.Tests
{
	[TestFixture]
	public class CouchbaseJsonExtensionTests : CouchbaseClientTestsBase
	{
        /// <summary>
        /// @test: create a city object, store it in form of json against
        /// a unique key, then get the value, it should be saved as type string
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if result is of type string
        /// </summary>
		[Test]
		public void When_Saving_City_With_JsonStore_City_Is_Stored_As_String()
		{
			var city = new City { Name = "Hartford", State = "CT", Type = "city" };
			var result = _Client.StoreJson(StoreMode.Set, "city_Hartford_CT", city);
			Assert.That(result, Is.True);

			var savedCity = _Client.Get("city_Hartford_CT");
			Assert.That(savedCity, Is.InstanceOf<string>());
		}

        /// <summary>
        /// @test: create a city object, store it in form of json against
        /// a unique key, then get the Json object as return value
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if result is of type json object
        /// </summary>
		[Test]
		public void When_Getting_City_With_JsonGet_City_Is_Returned_As_City()
		{
			var city = new City { Name = "Cambridge", State = "MA", Type = "city" };
			var result = _Client.StoreJson(StoreMode.Set, "city_Cambridge_MA", city);
			Assert.That(result, Is.True);

			var savedCity = _Client.GetJson<City>("city_Cambridge_MA");
			Assert.That(savedCity, Is.InstanceOf<City>());
			Assert.That(savedCity.Name, Is.StringMatching("Cambridge"));
			Assert.That(savedCity.Type, Is.StringMatching("city"));
			Assert.That(savedCity.State, Is.StringMatching("MA"));
		}

		private class City
		{
			[JsonProperty("name")]
			public string Name { get; set; }

			[JsonProperty("state")]
			public string State { get; set; }

			[JsonProperty("type")]
			public string Type { get; set; }
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