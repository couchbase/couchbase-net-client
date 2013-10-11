using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Couchbase.Tests.Factories;
using Couchbase.Tests.Utils;
using NUnit.Framework;
using Enyim.Caching.Memcached;
using Enyim.Caching.Memcached.Results;

namespace Couchbase.Tests
{
	[TestFixture(Description = "MemcachedClient Store Tests")]
	public class CouchbaseClientCasTests : CouchbaseClientTestsBase
	{
        /// <summary>
        /// @test: Store a randomly generated unique key-value, use he CAS value
        /// returned from the Store(), then use ExecuteCas() method
        /// to compare and set a value using the specified key and return the store operation result
        /// @pre: No section required in App.config file
        /// @post: Test passes if successfully able to add a key-value pair and then retrieve the same cas value,
        /// fails otherwise
        /// </summary>
		[Test]
		public void When_Storing_Item_With_Valid_Cas_Result_Is_Successful()
		{
            var key = TestUtils.GetUniqueKey("cas");
            var value = TestUtils.GetRandomString();
            var storeResult = TestUtils.Store(Client, StoreMode.Add, key, value);
            TestUtils.StoreAssertPass(storeResult);

            var casResult = Client.ExecuteCas(StoreMode.Set, key, value, storeResult.Cas);
            TestUtils.StoreAssertPass(casResult);
		}

        /// <summary>
        /// @test: Store a randomly generated unique key-value, use the different 
        /// cas value in ExecuteCas() mehod than what is returned by Store() method, 
        /// the ExecuteCas() fails due to invalid cas passed
        /// @pre: No section required in App.config file
        /// @post: Test passes if ExecuteCas() fals due to invalid cas, fails if ExecuteCas() passes
        /// </summary>
		[Test]
		public void When_Storing_Item_With_Invalid_Cas_Result_Is_Not_Successful()
		{
            var key = TestUtils.GetUniqueKey("cas");
            var value = TestUtils.GetRandomString();
            var storeResult = TestUtils.Store(Client, StoreMode.Add, key, value);
            TestUtils.StoreAssertPass(storeResult);

            var casResult = Client.ExecuteCas(StoreMode.Set, key, value, storeResult.Cas - 1);
            TestUtils.StoreAssertFail(casResult);
		}

		/// <summary>
		/// @test: Store a randomly generated unique key-value, use the CAS value
		/// returned from the Store(), then use ExecuteCas() method
		/// to replace the value using the specified key and return the store operation result
		/// @pre: No section required in App.config file
		/// @post: Test passes if successfully able to replace a key-value pair, fails otherwise
		/// </summary>
		[Test]
		public void When_Replacing_Item_With_Valid_Cas_Result_Is_Successful()
		{
            var key = TestUtils.GetUniqueKey("cas");
            var value = TestUtils.GetRandomString();
            var storeResult = TestUtils.Store(Client, StoreMode.Add, key, value);
            TestUtils.StoreAssertPass(storeResult);

            var casResult = Client.ExecuteCas(StoreMode.Replace, key, value, storeResult.Cas);
            TestUtils.StoreAssertPass(casResult);
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
