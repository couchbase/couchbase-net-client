using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Couchbase.Extensions;
using Enyim.Caching.Memcached;

namespace Couchbase.Tests
{
	[TestFixture]
	public class CouchbaseClientExtensionsTests : CouchbaseClientTestsBase
	{
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
	}

	internal class Thing
	{
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