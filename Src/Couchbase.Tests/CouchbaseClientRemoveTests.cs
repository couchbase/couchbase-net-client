﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Couchbase.Tests.Factories;
using Couchbase.Tests.Utils;
using NUnit.Framework;
using Couchbase.Configuration;
using Enyim.Caching.Memcached;
using Couchbase.Constants;
using Enyim.Caching.Memcached.Results.StatusCodes;

namespace Couchbase.Tests
{
    [TestFixture]
    public class CouchbaseClientRemoveTests : CouchbaseClientTestsBase
    {
        /// <summary>
        /// @test: Store a randomly generated key, remove that key and then get value
        /// agsinst the same key, the Get operation should fail as key is already deleted
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if store and deletion operation are success and Get operation
        /// must fail ideally
        /// </summary>
        [Test]
        public void When_Removing_A_Valid_Key_Result_Is_Successful()
        {
            var key = TestUtils.GetUniqueKey("remove");
            var storeResult = TestUtils.Store(Client,key: key);
            TestUtils.StoreAssertPass(storeResult);

            var removeResult = Client.ExecuteRemove(key);
            Assert.That(removeResult.Success, Is.True, "Success was false");
            Assert.That(removeResult.StatusCode, Is.Null.Or.EqualTo(0), "StatusCode was neither null nor 0");

            var getResult = Client.ExecuteGet(key);
            TestUtils.GetAssertFail(getResult);
        }

        /// <summary>
        /// @test: Deleting a key which does not exist will fail the Remove operation
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if Remove operation fails
        /// </summary>
        [Test]
        public void When_Removing_An_Invalid_Key_Result_Is_Not_Successful()
        {
            var key = TestUtils.GetUniqueKey("remove");

            var removeResult = Client.ExecuteRemove(key);
            Assert.That(removeResult.Success, Is.False, "Success was true");
            Assert.That(removeResult.StatusCode, Is.EqualTo((int)StatusCodeEnums.NotFound), "Status code was not NotFound");
        }

        [Test]
        public void When_Removing_A_Key_From_A_Down_Node_No_Exception_Is_Thrown_And_Success_Is_False()
        {
            var config = new CouchbaseClientConfiguration();
            config.Urls.Add(new Uri("http://doesnotexist:8091/pools/"));
            config.Bucket = "default";

            using (var client = new CouchbaseClient(config))
            {
                var removeResult = client.ExecuteRemove("foo");
                Assert.That(removeResult.Success, Is.False);
                Assert.That(removeResult.Message, Is.StringContaining(ClientErrors.FAILURE_NODE_NOT_FOUND));
            }
        }

        [Test]
        public void When_ExecuteRemoving_A_Key_With_A_Valid_Cas_Result_Success_Is_True()
        {
            var key = TestUtils.GetUniqueKey("remove");
            var storeResult = TestUtils.Store(Client, key: key);
            TestUtils.StoreAssertPass(storeResult);

            var removeResult = Client.ExecuteRemove(key, storeResult.Cas);
            Assert.That(removeResult.Success, Is.True, "Success was false");
            Assert.That(removeResult.StatusCode, Is.Null.Or.EqualTo(0), "StatusCode was neither null nor 0");

            var getResult = Client.ExecuteGet(key);
            TestUtils.GetAssertFail(getResult);
        }

        [Test]
        public void When_ExecuteRemoving_A_Key_With_An_Invalid_Cas_Result_Success_Is_False()
        {
            var key = TestUtils.GetUniqueKey("remove");
            var storeResult = TestUtils.Store(Client, key: key);
            TestUtils.StoreAssertPass(storeResult);

            var removeResult = Client.ExecuteRemove(key, storeResult.Cas-1);
            Assert.That(removeResult.Success, Is.False, "Success was false");
            Assert.That(removeResult.StatusCode, Is.EqualTo((int)StatusCodeEnums.DataExistsForKey), "StatusCode was not 2");
        }

        [Test]
        public void When_Removing_A_Key_With_A_Valid_Cas_Success_Is_True()
        {
            var key = TestUtils.GetUniqueKey("remove");
            var storeResult = TestUtils.Store(Client, key: key);
            TestUtils.StoreAssertPass(storeResult);

            var removeResult = Client.Remove(key, storeResult.Cas);
            Assert.That(removeResult, Is.True, "Success was false");

            var getResult = Client.ExecuteGet(key);
            TestUtils.GetAssertFail(getResult);
        }

        [Test]
        public void When_Removing_A_Key_With_An_Invalid_Cas_Success_Is_False()
        {
            var key = TestUtils.GetUniqueKey("remove");
            var storeResult = TestUtils.Store(Client, key: key);
            TestUtils.StoreAssertPass(storeResult);

            var removeResult = Client.Remove(key, storeResult.Cas - 1);
            Assert.That(removeResult, Is.False, "Success was false");
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