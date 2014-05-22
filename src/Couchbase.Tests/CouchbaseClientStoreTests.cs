using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Couchbase.Tests.Utils;
using NUnit.Framework;
using Enyim.Caching.Memcached;
using Enyim.Caching.Memcached.Results;
using Couchbase.Configuration;
using Couchbase.Constants;

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
            var result = TestUtils.Store(Client, StoreMode.Add);
            TestUtils.StoreAssertPass(result);
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
            var key = TestUtils.GetUniqueKey("store");
            var result = TestUtils.Store(Client, StoreMode.Add, key);
            TestUtils.StoreAssertPass(result);

            result = TestUtils.Store(Client, StoreMode.Add, key);
            TestUtils.StoreAssertFail(result);
        }

        /// <summary>
        /// @test: Store a new key with store mode replace will fail as the key does not exist
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if replace fails
        /// </summary>
        [Test]
        public void When_Storing_Item_With_New_Key_And_StoreMode_Replace_Result_Is_Not_Successful()
        {
            var result = TestUtils.Store(Client, StoreMode.Replace);
            TestUtils.StoreAssertFail(result);
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
            var key = TestUtils.GetUniqueKey("store");
            var result = TestUtils.Store(Client, StoreMode.Add, key);
            TestUtils.StoreAssertPass(result);

            result = TestUtils.Store(Client, StoreMode.Replace, key);
            TestUtils.StoreAssertPass(result);
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
            var result = TestUtils.Store(Client, StoreMode.Set);
            TestUtils.StoreAssertPass(result);
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
            var key = TestUtils.GetUniqueKey("store");
            var result = TestUtils.Store(Client, StoreMode.Add, key);
            TestUtils.StoreAssertPass(result);

            result = TestUtils.Store(Client, StoreMode.Set, key);
            TestUtils.StoreAssertPass(result);
        }

        [Test]
        public void When_Storing_A_Key_From_A_Down_Node_No_Exception_Is_Thrown_And_Success_Is_False()
        {
            var config = new CouchbaseClientConfiguration();
            config.Urls.Add(new Uri("http://doesnotexist:8091/pools/"));
            config.Bucket = "default";

            using (var client = new CouchbaseClient(config))
            {
                var storeResult = client.ExecuteStore(StoreMode.Set, "foo", "bar");
                Assert.That(storeResult.Success, Is.False);
                Assert.That(storeResult.Message, Is.StringContaining(ClientErrors.FAILURE_NODE_NOT_FOUND));
            }
        }

        /// <summary>
        /// @test: Store a document with a small TTL and assert that the TTL is adhered to by waiting and storing again.
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if the both store calls are successful.
        /// </summary>
        [Test]
        public void Storing_Document_With_Small_TTL()
        {
            var document = @"{""name"": ""test_document""}";
            int ttl = 5; // seconds for the TTL
            var result1 = TestUtils.Store(Client, TimeSpan.FromSeconds(ttl), StoreMode.Add, "key_ttl_test", document);
            System.Threading.Thread.Sleep(1000 * (ttl*2)); // Sleep for longer than the TTL.
            var result2 = TestUtils.Store(Client, TimeSpan.FromSeconds(ttl), StoreMode.Add, "key_ttl_test", document);
            TestUtils.StoreAssertPass(result1);
            TestUtils.StoreAssertPass(result2);
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