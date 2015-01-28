﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Couchbase.Tests.Factories;
using Couchbase.Tests.Utils;
using Enyim;
using Enyim.Caching.Memcached;
using Enyim.Caching.Memcached.Results;
using NUnit.Framework;
using Enyim.Caching.Memcached.Results.StatusCodes;
using Couchbase.Configuration;
using Couchbase.Constants;

namespace Couchbase.Tests
{
    [TestFixture]
    public class CouchbaseClientGetTests : CouchbaseClientTestsBase
    {
        /// <summary>
        /// @test: Store a key-value in data bucket. Then get the value associated with the given key
        /// @pre: Default configuration to initialize client  in App.config
        /// @post: Test passes if successfully stores key-value and then able to get value, fails otherwise
        /// </summary>
        [Test]
        public void When_Getting_Existing_Item_Value_Is_Not_Null_And_Result_Is_Successful()
        {
            var key = TestUtils.GetUniqueKey("get");
            var value = TestUtils.GetRandomString();
            var storeResult = TestUtils.Store(Client, key: key, value: value);
            TestUtils.StoreAssertPass(storeResult);

            var getResult = Client.ExecuteGet(key);
            TestUtils.GetAssertPass(getResult, value);
        }

        /// <summary>
        /// @test: Getting a value for key that is invalid and does not exist, the test would fail
        /// @pre: Default configuration to initialize client  in App.config
        /// @post: Test passes if Get method to retrive the value for invalid key fails
        /// </summary>
        [Test]
        public void When_Getting_Item_For_Invalid_Key_HasValue_Is_False_And_Result_Is_Not_Successful()
        {
            var key = TestUtils.GetUniqueKey("get");

            var getResult = Client.ExecuteGet(key);
            Assert.That(getResult.StatusCode, Is.EqualTo((int)StatusCodeEnums.NotFound));
            TestUtils.GetAssertFail(getResult);
        }

        /// <summary>
        /// @test: Store a key-value in data bucket. Then try-get the value associated with the given key
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if successfully stores key-value and then get value, fails otherwise
        /// </summary>
        [Test]
        public void When_TryGetting_Existing_Item_Value_Is_Not_Null_And_Result_Is_Successful()
        {
            var key = TestUtils.GetUniqueKey("get");
            var value = TestUtils.GetRandomString();
            var storeResult = TestUtils.Store(Client, key: key, value: value);
            TestUtils.StoreAssertPass(storeResult);

            object temp;
            var getResult = Client.ExecuteTryGet(key, out temp);
            TestUtils.GetAssertPass(getResult, temp);
        }

        /// <summary>
        /// @test: Store a key-value in data bucket. Use generic type string.
        /// Then get the value associated with the given key
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if successfully stores key-value and then able to get value, fails otherwise
        /// </summary>
        [Test]
        public void When_Generic_Getting_Existing_Item_Value_Is_Not_Null_And_Result_Is_Successful()
        {
            var key = TestUtils.GetUniqueKey("get");
            var value = TestUtils.GetRandomString();
            var storeResult = TestUtils.Store(Client, key: key, value: value);
            TestUtils.StoreAssertPass(storeResult);

            var getResult = Client.ExecuteGet<string>(key);
            Assert.That(getResult.Success, Is.True, "Success was false");
            Assert.That(getResult.Cas, Is.GreaterThan(0), "Cas value was 0");
            Assert.That(getResult.StatusCode, Is.EqualTo(0).Or.Null, "StatusCode was neither 0 nor null");
            Assert.That(getResult.Value, Is.EqualTo(value), "Actual value was not expected value: " + getResult.Value);
            Assert.That(getResult.Value, Is.InstanceOf<string>(), "Value was not a string");
        }

        /// <summary>
        /// @test: Store multiple keys in data bucket. Then get the keys and verify the count
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if successfully stores all keys and count is correct
        /// </summary>
        [Test]
        public void When_Getting_Multiple_Existing_Keys_Result_Is_Successful()
        {
            var keys = TestUtils.GetUniqueKeys().Distinct();
            foreach (var key in keys)
            {
                TestUtils.Store(Client, key: key, value: "Value for" + key);
            }

            var dict = Client.ExecuteGet(keys);
            Assert.That(dict.Keys.Count, Is.EqualTo(keys.Count()), "Keys count did not match results count");

            foreach (var key in dict.Keys)
            {
                Assert.That(dict[key].Success, Is.True, "Get failed for key: " + key);
            }
        }

        /// <summary>
        /// @test: Get unique set of keys but do not store them in bucket. Get the keys and verify the count
        /// should be zero and no key should be found as a result of get operation
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if no key is found
        /// </summary>
        [Test]
        public void When_Getting_Multiple_Non_Existent_Keys_Result_Is_Not_Successful()
        {
            var keys = TestUtils.GetUniqueKeys().Distinct();

            var results = Client.ExecuteGet(keys);
            foreach (var result in results)
            {
                Assert.AreEqual(result.Value.StatusCode, StatusCode.KeyNotFound.ToInt());
            }
        }

        /// <summary>
        /// @test: Store a key-value in data bucket. Then get the value associated with the given key
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if successfully stores key-value and then able to get value
        /// when expiration is not null
        /// </summary>
        [Test]
        public void When_Getting_Existing_Item_Value_With_Expiration_Is_Not_Null_And_Result_Is_Successful()
        {
            var key = TestUtils.GetUniqueKey("get");
            var value = TestUtils.GetRandomString();
            var storeResult = TestUtils.Store(Client, key: key, value: value);
            TestUtils.StoreAssertPass(storeResult);

            var getResult = Client.ExecuteGet(key, DateTime.Now.AddSeconds(10));
            TestUtils.GetAssertPass(getResult, value);
        }

        /// <summary>
        /// @test: Get the value for a key that does not exist, the get operation should fail
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if get operation fails
        /// </summary>
        [Test]
        public void When_Getting_Item_For_With_Expiration_And_Invalid_Key_HasValue_Is_False_And_Result_Is_Not_Successful()
        {
            var key = TestUtils.GetUniqueKey("get");

            var getResult = Client.ExecuteGet(key, DateTime.Now.AddSeconds(10));
            TestUtils.GetAssertFail(getResult);
        }

        /// <summary>
        /// @test: Store a key-value in data bucket. Then try-get the value with expiration
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if successfully stores key-value and then able to get value, fails otherwise
        /// </summary>
        [Test]
        public void When_TryGetting_Existing_Item_With_Expiration_Value_Is_Not_Null_And_Result_Is_Successful()
        {
            var key = TestUtils.GetUniqueKey("get");
            var value = TestUtils.GetRandomString();
            var storeResult = TestUtils.Store(Client, key: key, value: value);
            TestUtils.StoreAssertPass(storeResult);

            object temp;
            var getResult = Client.ExecuteTryGet(key, DateTime.Now.AddSeconds(10), out temp);
            TestUtils.GetAssertPass(getResult, temp);
        }

        /// <summary>
        /// @test: Store a key-value in data bucket. Then get the value using generic type as string
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if successfully stores key-value and then able to get value, fails otherwise
        /// </summary>
        [Test]
        public void When_Generic_Getting_Existing_Item_With_Expiration_Value_Is_Not_Null_And_Result_Is_Successful()
        {
            var key = TestUtils.GetUniqueKey("get");
            var value = TestUtils.GetRandomString();
            var storeResult = TestUtils.Store(Client, key: key, value: value);
            TestUtils.StoreAssertPass(storeResult);

            var getResult = Client.ExecuteGet<string>(key, DateTime.Now.AddSeconds(10));
            Assert.That(getResult.Success, Is.True, "Success was false");
            Assert.That(getResult.Cas, Is.GreaterThan(0), "Cas value was 0");
            Assert.That(getResult.StatusCode, Is.EqualTo(0).Or.Null, "StatusCode was neither 0 nor null");
            Assert.That(getResult.Value, Is.EqualTo(value), "Actual value was not expected value: " + getResult.Value);
            Assert.That(getResult.Value, Is.InstanceOf<string>(), "Value was not a string");
        }

        [Test]
        public void When_Getting_A_Key_From_A_Down_Node_No_Exception_Is_Thrown_And_Success_Is_False()
        {
            var config = new CouchbaseClientConfiguration();
            config.Urls.Add(new Uri("http://doesnotexist:8091/pools/"));
            config.Bucket = "default";

            using (var client = new CouchbaseClient(config))
            {
                var getResult = client.ExecuteGet("foo");
                Assert.That(getResult.Success, Is.False);
                Assert.That(getResult.Message, Is.StringContaining(ClientErrors.FAILURE_NODE_NOT_FOUND));
            }
        }

        [Test]
        public void When_ExecuteGet_Fails_The_Correct_KeyDoesNotExist_Is_Returned()
        {
            var res1 = Client.ExecuteGet("DoesNotExist");
            Assert.IsFalse(res1.Success);
            Assert.AreEqual(res1.StatusCode, 1);

            var res2 = Client.ExecuteGet("DoesNotExist", DateTime.UtcNow.AddMinutes(10));
            Assert.IsFalse(res2.Success);
            Assert.AreEqual(res2.StatusCode, 1);
        }

        [Test]
        public void When_ExecuteGet_Is_Called_With_List_Of_Keys_All_Are_Returned()
        {
            var keys = new List<string>(10);
            for (int i = 0; i < 10; i++)
            {
                var key = string.Format("executegetkey{0}", i);
                var value = string.Format("executegetvalue{0}", i);

                if (Client.Store(StoreMode.Set, key, value))
                {
                    keys.Add(key);
                }
            }
            var results = Client.ExecuteGet(keys);
            foreach (var result in results)
            {
                Assert.IsTrue(result.Value.Success);
            }
        }

        [Test]
        public void When_ExecuteGet_Is_Called_With_Invalid_Keys_Errors_Are_Returned()
        {
            var keysThatDoNotExist = new[] { "not-exist1", "not-exist2" };
            var results = Client.ExecuteGet(keysThatDoNotExist);
            Assert.AreEqual(keysThatDoNotExist.Count(), results.Count);

            foreach (var result in results)
            {
                Assert.IsFalse(result.Value.HasValue);
                Assert.IsFalse(result.Value.Success);
                Assert.AreEqual(StatusCode.KeyNotFound.ToInt(), result.Value.StatusCode);
            }
        }

        [Test]
        public void When_ExecuteGet_Is_Called_With_Valid_Keys_No_Errors_Are_Returned()
        {
            var keysThatExist = new[] { "exist1", "exist2" };
            foreach (var key in keysThatExist)
            {
                var result = Client.Store(StoreMode.Set, key, key);
                Assert.IsTrue(result);
            }
            var results = Client.ExecuteGet(keysThatExist);
            Assert.AreEqual(keysThatExist.Count(), results.Count);

            foreach (var result in results)
            {
                Assert.IsTrue(result.Value.HasValue);
                Assert.IsTrue(result.Value.Success);
                Assert.AreEqual(StatusCode.Success.ToInt(), result.Value.StatusCode);
            }
        }

        [Test]
        public void When_ExecuteGet_Is_Called_With_Invalid_Configuration_Errors_Are_Returned()
        {
            using (var client = new CouchbaseClient("bad-config"))
            {
                var keys = new[] { "exist1", "exist2" };
                var results = client.ExecuteGet(keys);
                Assert.AreEqual(keys.Count(), results.Count);

                foreach (var result in results)
                {
                    Assert.IsFalse(result.Value.HasValue);
                    Assert.IsFalse(result.Value.Success);
                    Assert.AreEqual(StatusCode.UnableToLocateNode.ToInt(), result.Value.StatusCode);
                }
            }
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