using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Enyim.Caching.Memcached.Results.StatusCodes;

namespace Couchbase.Tests
{
	[TestFixture]
	public class CouchbaseClientGetTests : CouchbaseClientTestsBase
	{

		[Test]
		public void When_Getting_Existing_Item_Value_Is_Not_Null_And_Result_Is_Successful()
		{
			var key = GetUniqueKey("get");
			var value = GetRandomString();
			var storeResult = Store(key: key, value: value);
			StoreAssertPass(storeResult);

			var getResult = _Client.ExecuteGet(key);
			GetAssertPass(getResult, value);
		}

		[Test]
		public void When_Getting_Item_For_Invalid_Key_HasValue_Is_False_And_Result_Is_Not_Successful()
		{
			var key = GetUniqueKey("get");

			var getResult = _Client.ExecuteGet(key);
			Assert.That(getResult.StatusCode, Is.EqualTo((int)StatusCodeEnums.NotFound));
			GetAssertFail(getResult);
		}

		[Test]
		public void When_TryGetting_Existing_Item_Value_Is_Not_Null_And_Result_Is_Successful()
		{
			var key = GetUniqueKey("get");
			var value = GetRandomString();
			var storeResult = Store(key: key, value: value);
			StoreAssertPass(storeResult);

			object temp;
			var getResult = _Client.ExecuteTryGet(key, out temp);
			GetAssertPass(getResult, temp);
		}

		[Test]
		public void When_Generic_Getting_Existing_Item_Value_Is_Not_Null_And_Result_Is_Successful()
		{
			var key = GetUniqueKey("get");
			var value = GetRandomString();
			var storeResult = Store(key: key, value: value);
			StoreAssertPass(storeResult);

			var getResult = _Client.ExecuteGet<string>(key);
			Assert.That(getResult.Success, Is.True, "Success was false");
			Assert.That(getResult.Cas, Is.GreaterThan(0), "Cas value was 0");
			Assert.That(getResult.StatusCode, Is.EqualTo(0).Or.Null, "StatusCode was neither 0 nor null");
			Assert.That(getResult.Value, Is.EqualTo(value), "Actual value was not expected value: " + getResult.Value);
			Assert.That(getResult.Value, Is.InstanceOf<string>(), "Value was not a string");
		}

		[Test]
		public void When_Getting_Multiple_Keys_Result_Is_Successful()
		{
			var keys = GetUniqueKeys().Distinct();
			foreach (var key in keys)
			{
				Store(key: key, value: "Value for" + key);
			}

			var dict = _Client.ExecuteGet(keys);
			Assert.That(dict.Keys.Count, Is.EqualTo(keys.Count()), "Keys count did not match results count");

			foreach (var key in dict.Keys)
			{
				Assert.That(dict[key].Success, Is.True, "Get failed for key: " + key);
			}
		}

		[Test]
		public void When_Getting_Existing_Item_Value_With_Expiration_Is_Not_Null_And_Result_Is_Successful()
		{
			var key = GetUniqueKey("get");
			var value = GetRandomString();
			var storeResult = Store(key: key, value: value);
			StoreAssertPass(storeResult);

			var getResult = _Client.ExecuteGet(key, DateTime.Now.AddSeconds(10));
			GetAssertPass(getResult, value);
		}

		[Test]
		public void When_Getting_Item_For_With_Expiration_And_Invalid_Key_HasValue_Is_False_And_Result_Is_Not_Successful()
		{
			var key = GetUniqueKey("get");

			var getResult = _Client.ExecuteGet(key, DateTime.Now.AddSeconds(10));
			GetAssertFail(getResult);
		}

		[Test]
		public void When_TryGetting_Existing_Item_With_Expiration_Value_Is_Not_Null_And_Result_Is_Successful()
		{
			var key = GetUniqueKey("get");
			var value = GetRandomString();
			var storeResult = Store(key: key, value: value);
			StoreAssertPass(storeResult);

			object temp;
			var getResult = _Client.ExecuteTryGet(key, DateTime.Now.AddSeconds(10), out temp);
			GetAssertPass(getResult, temp);
		}

		[Test]
		public void When_Generic_Getting_Existing_Item_With_Expiration_Value_Is_Not_Null_And_Result_Is_Successful()
		{
			var key = GetUniqueKey("get");
			var value = GetRandomString();
			var storeResult = Store(key: key, value: value);
			StoreAssertPass(storeResult);

			var getResult = _Client.ExecuteGet<string>(key, DateTime.Now.AddSeconds(10));
			Assert.That(getResult.Success, Is.True, "Success was false");
			Assert.That(getResult.Cas, Is.GreaterThan(0), "Cas value was 0");
			Assert.That(getResult.StatusCode, Is.EqualTo(0).Or.Null, "StatusCode was neither 0 nor null");
			Assert.That(getResult.Value, Is.EqualTo(value), "Actual value was not expected value: " + getResult.Value);
			Assert.That(getResult.Value, Is.InstanceOf<string>(), "Value was not a string");
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