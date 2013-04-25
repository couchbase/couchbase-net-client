using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Couchbase.Extensions;
using Enyim.Caching.Memcached;
using Couchbase.Tests.Utils;
using Couchbase.Operations;

namespace Couchbase.Tests
{
	[TestFixture]
	public class CouchbaseClientExtensionsTests : CouchbaseClientTestsBase
	{
        /// <summary>
        /// @test: Generate a unique key and store key using StoreJson(), without converting into json
        /// the store operation would fail
        /// @pre: Generate a unique key and data to append in that key
        /// @post: Test passes if ExecuteAppend() passes
        /// </summary>
		[Test]
		public void When_Serializing_Class_Without_Json_Property_Attributes_Properties_Are_Camel_Cased()
		{
			var thing = new Thing();
			var key = GetUniqueKey();
			var result = _Client.StoreJson(StoreMode.Set, key, thing);

			Assert.That(result, Is.True, "Store failed");

			var savedThing = _Client.Get<string>(key);
			Assert.That(savedThing, Is.StringContaining("someProperty").And.StringContaining("someOtherProperty"));
		}

        /// <summary>
        /// @test: Generate a unique key and map the properties, store the json format data. 
        /// Then get the json data and deserialize. the properties would map
        /// @pre: set properties to random values before storing data
        /// @post: Test passes properties are correctly mapped
        /// </summary>
		[Test]
		public void When_Deserializing_Class_Without_Json_Property_Attributes_Camel_Cased_Properties_Are_Mapped()
		{
			var thing = new Thing { SomeProperty = "foo", SomeOtherProperty = 1 };
			var key = GetUniqueKey();
			var result = _Client.StoreJson(StoreMode.Set, key, thing);

			Assert.That(result, Is.True, "Store failed");

			var savedThing = _Client.GetJson<Thing>(key);
			Assert.That(savedThing.SomeProperty, Is.StringMatching("foo"));
			Assert.That(savedThing.SomeOtherProperty, Is.EqualTo(1));

		}

		[Test]
		public void When_StoringJson_For_A_Class_With_Id_Property_Id_Is_Not_Stored_In_Json()
		{
			var key = KeyValueUtils.GenerateKey("json");

			var thing = new Thing { Id = key, SomeProperty = "Foo", SomeOtherProperty = 17 };
			var result = _Client.StoreJson(StoreMode.Set, key, thing);
			Assert.That(result, Is.True);

			var obj = _Client.Get<string>(key);
			Assert.That(obj, Is.Not.StringContaining("\"id\""));

			var savedThing = _Client.GetJson<Thing>(key);
			Assert.That(savedThing.Id, Is.StringContaining(key));
		}

		[Test]
		public void When_ExecuteStoringJson_For_A_Class_With_Id_Property_Id_Is_Not_Stored_In_Json()
		{
			var key = KeyValueUtils.GenerateKey("json");

			var thing = new Thing { Id = key, SomeProperty = "Foo", SomeOtherProperty = 17 };
			var result = _Client.ExecuteStoreJson(StoreMode.Set, key, thing);
			Assert.That(result.Success, Is.True);

			var obj = _Client.ExecuteGet<string>(key);
			Assert.That(obj.Value, Is.Not.StringContaining("\"id\""));

			var savedThing = _Client.ExecuteGetJson<Thing>(key);
			Assert.That(savedThing.Value.Id, Is.StringContaining(key));
		}

		[Test]
		public void When_Persisting_Json_Id_Is_Not_Serializd_But_Is_Returned_In_View()
		{
		    var key = KeyValueUtils.GenerateKey("json");

		    var thing = new Thing { Id = key, SomeProperty = "Foo", SomeOtherProperty = 17 };
		    var result = _Client.ExecuteStoreJson(StoreMode.Set, key, thing, PersistTo.One);
		    Assert.That(result.Success, Is.True);

		    var obj = _Client.ExecuteGet<string>(key);
		    Assert.That(obj.Value, Is.Not.StringContaining("\"id\""));

		    var savedThing = _Client.ExecuteGetJson<Thing>(key);
		    Assert.That(savedThing.Value.Id, Is.StringContaining(key));

			var view = _Client.GetView<Thing>("things", "all", true);
			foreach (var item in view)
			{
				if (item.Id == key)
				{
					Assert.Pass();
					return;
				}
			}

			Assert.Fail("Id was not returned in view");
		}
	}

	internal class Thing
	{
		public string Id { get; set; }

		public string SomeProperty { get; set; }

		public int SomeOtherProperty { get; set; }
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