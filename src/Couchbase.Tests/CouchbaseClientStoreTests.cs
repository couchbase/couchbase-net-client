﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Enyim.Caching.Memcached;
using Enyim.Caching.Memcached.Results;

namespace Couchbase.Tests
{

	[TestFixture(Description = "MemcachedClient Store Tests")]
	public class CouchbaseClientStoreTests : CouchbaseClientTestsBase
	{
        /// <summary>
        /// @test: Store a randomly generated key, with store mode Add, new key is added
        /// @pre: Default configuration to initialize client in app.config 
        /// @post: Test passes if new key is added successfully
        /// </summary>
		[Test]
		public void When_Storing_Item_With_New_Key_And_StoreMode_Add_Result_Is_Successful()
		{
			var result = Store(StoreMode.Add);
			StoreAssertPass(result);
		}

        /// <summary>
        /// @test: Store a randomly generated key, store again using same key with Add operation,
        /// the result will not be successful
        /// @pre: Default configuration to initialize client in app.config 
        /// @post: Test passes if new key is added first time and second time it fails
        /// </summary>
		[Test]
		public void When_Storing_Item_With_Existing_Key_And_StoreMode_Add_Result_Is_Not_Successful()
		{
			var key = GetUniqueKey("store");
			var result = Store(StoreMode.Add, key);
			StoreAssertPass(result);

			result = Store(StoreMode.Add, key);
			StoreAssertFail(result);
		}

        /// <summary>
        /// @test: Store a new key with store mode replace will fail as the key does not exist
        /// @pre: Default configuration to initialize client in app.config 
        /// @post: Test passes if replace fails
        /// </summary>
		[Test]
		public void When_Storing_Item_With_New_Key_And_StoreMode_Replace_Result_Is_Not_Successful()
		{
			var result = Store(StoreMode.Replace);
			StoreAssertFail(result);

		}

        /// <summary>
        /// @test: Store a randomly generated key, with store mode Add, then change the key
        /// with store mode replace, key should be replaced successfully
        /// @pre: Default configuration to initialize client in app.config 
        /// @post: Test passes if new key is added and later replaced successfully
        /// </summary>
		[Test]
		public void When_Storing_Item_With_Existing_Key_And_StoreMode_Replace_Result_Is_Successful()
		{
			var key = GetUniqueKey("store");
			var result = Store(StoreMode.Add, key);
			StoreAssertPass(result);

			result = Store(StoreMode.Replace, key);
			StoreAssertPass(result);
		}

        /// <summary>
        /// @test: Store a randomly generated key, with store mode Set, 
        /// new key should be stored correctly
        /// @pre: Default configuration to initialize client in app.config 
        /// @post: Test passes if new key is stored successfully
        /// </summary>
		[Test]
		public void When_Storing_Item_With_New_Key_And_StoreMode_Set_Result_Is_Successful()
		{
			var result = Store(StoreMode.Set);
			StoreAssertPass(result);

		}

        /// <summary>
        /// @test: Store a randomly generated key, with store mode Add, then change the key
        /// with store mode set, key should be set successfully
        /// @pre: Default configuration to initialize client in app.config 
        /// @post: Test passes if new key is stored successfully
        /// </summary>
		[Test]
		public void When_Storing_Item_With_Existing_Key_And_StoreMode_Set_Result_Is_Successful()
		{
			var key = GetUniqueKey("store");
			var result = Store(StoreMode.Add, key);
			StoreAssertPass(result);

			result = Store(StoreMode.Set, key);
			StoreAssertPass(result);
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
