using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Couchbase.Tests.Factories;
using NUnit.Framework;
using Newtonsoft.Json;
using Couchbase.Extensions;
using Enyim.Caching.Memcached;
using System.Threading;

namespace Couchbase.Tests
{
    [TestFixture]
    public class CouchbaseJsonExtensionTests : CouchbaseClientTestsBase
    {
        #region Store tests

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
            var result = Client.StoreJson(StoreMode.Set, "city_Hartford_CT", city);
            Assert.That(result, Is.True);

            var savedCity = Client.Get("city_Hartford_CT");
            Assert.That(savedCity, Is.InstanceOf<string>());
        }

        /// <summary>
        /// @test: create a city object, store it in form of json against
        /// a unique key, then get the value, it should be saved as type string
        /// After expiry, item should not be valid
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if result is of type string
        /// </summary>
        [Test]
        public void When_Saving_City_With_JsonStore_And_DateTime_Expiry_Item_Expires()
        {
            var city = new City { Name = "Hartford", State = "CT", Type = "city" };
            var result = Client.ExecuteStoreJson(StoreMode.Set, "city_Hartford_CT_Exp", city, DateTime.Now.AddSeconds(2));
            Assert.That(result.Success, Is.True, result.Message);
            Thread.Sleep(3000);
            var savedCity = Client.Get("city_Hartford_CT_Exp");
            Assert.That(savedCity, Is.Null);
        }

        /// <summary>
        /// @test: create a city object, store it in form of json against
        /// a unique key, then get the value, it should be saved as type string
        /// When passing valid Cas, operation is successful
        /// After expiry, item should not be valid
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if result is of type string
        /// </summary>
        [Test]
        public void When_Saving_City_With_JsonStore_City_And_TimeSpan_Expiry_Item_Expires()
        {
            var city = new City { Name = "Hartford", State = "CT", Type = "city" };
            var result = Client.ExecuteStoreJson(StoreMode.Set, "city_Hartford_CT_Exp", city, TimeSpan.FromSeconds(2));
            Assert.That(result.Success, Is.True);
            Thread.Sleep(3000);
            var savedCity = Client.Get("city_Hartford_CT_Exp");
            Assert.That(savedCity, Is.Null);
        }

        /// <summary>
        /// @test: create a city object, store it in form of json against
        /// a unique key, then get the value, it should be saved as type string
        /// After expiry, item should not be valid
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if result is of type string
        /// </summary>
        [Test]
        public void When_Saving_City_With_JsonStore_And_Valid_Cas_Store_Is_Successful()
        {
            var city = new City { Name = "Hartford", State = "CT", Type = "city" };
            var result = Client.ExecuteStoreJson(StoreMode.Set, "city_Hartford_CT", city, TimeSpan.FromSeconds(2));
            Assert.That(result.Success, Is.True);

            city.Name = "New Haven";
            var casResult = Client.ExecuteCasJson(StoreMode.Set, "city_Hartford_CT", city, result.Cas);
            Assert.That(casResult.Success, Is.True);
        }

        /// <summary>
        /// @test: create a city object, store it in form of json against
        /// a unique key, then get the value, it should be saved as type string
        /// After expiry, item should not be valid
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if result is of type string
        /// </summary>
        [Test]
        public void When_Saving_City_With_JsonStore_And_Invalid_Cas_Store_Is_Successful()
        {
            var city = new City { Name = "Hartford", State = "CT", Type = "city" };
            var result = Client.ExecuteStoreJson(StoreMode.Set, "city_Hartford_CT", city, TimeSpan.FromSeconds(2));
            Assert.That(result.Success, Is.True);

            city.Name = "New Haven";
            var casResult = Client.ExecuteCasJson(StoreMode.Set, "city_Hartford_CT", city, result.Cas-1);
            Assert.That(casResult.Success, Is.False);
        }

        #endregion

        #region Get extension tests

        /// <summary>
        /// @test: create a city object, store it in form of json against
        /// a unique key, then get the Json object as return IGetOperationResult
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if result is of type json object
        /// </summary>
        [Test]
        public void When_Execute_Getting_City_With_Json_Get_Result_Value_Is_Returned_As_City()
        {
            var city = new City { Name = "Boston", State = "MA", Type = "city" };
            var result = Client.ExecuteStoreJson(StoreMode.Set, "city_Boston_MA", city);
            Assert.That(result.Success, Is.True);

            var getResult = Client.ExecuteGetJson<City>("city_Boston_MA");
            Assert.That(getResult.Success, Is.True);
            Assert.That(getResult.Cas, Is.GreaterThan(0).And.EqualTo(result.Cas));

            Assert.That(getResult.Value, Is.InstanceOf<City>());
            Assert.That(getResult.Value.Name, Is.StringMatching("Boston"));
            Assert.That(getResult.Value.Type, Is.StringMatching("city"));
            Assert.That(getResult.Value.State, Is.StringMatching("MA"));
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
            var result = Client.StoreJson(StoreMode.Set, "city_Cambridge_MA", city);
            Assert.That(result, Is.True);

            var savedCity = Client.GetJson<City>("city_Cambridge_MA");
            Assert.That(savedCity, Is.InstanceOf<City>());
            Assert.That(savedCity.Name, Is.StringMatching("Cambridge"));
            Assert.That(savedCity.Type, Is.StringMatching("city"));
            Assert.That(savedCity.State, Is.StringMatching("MA"));
        }

        [Test]
        public void Test_That_StoreJson_Can_Store_Lists_As_Json()
        {
            var key = "Test_That_GetJson_Can_Retrieve_Lists_As_Json";
            var list = new List<int> {1, 2, 3, 4};
            var result = Client.StoreJson(StoreMode.Set, key, list);
            Assert.IsTrue(result);

            var output = Client.GetJson<List<int>>(key);
            Assert.AreEqual(list.Count, output.Count);
        }

        [Test]
        public void Test_That_StoreJson_Can_Store_Arrays_As_Json()
        {
            var key = "Test_That_GetJson_Can_Retrieve_Arrays_As_Json";
            var list = new []{ 1, 2, 3, 4 };
            var result = Client.StoreJson(StoreMode.Set, key, list);
            Assert.IsTrue(result);

            var output = Client.GetJson<int[]>(key);
            Assert.AreEqual(list.Count(), output.Count());
        }

        #endregion

        private class City
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("state")]
            public string State { get; set; }

            [JsonProperty("type")]
            public string Type { get; set; }

            [JsonProperty("lastUpdated")]
            public DateTime LastUpdated { get; set; }
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