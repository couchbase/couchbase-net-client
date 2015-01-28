﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Enyim.Caching.Memcached.Results;
using NUnit.Framework;
using Couchbase.Configuration;
using Enyim.Caching.Memcached;
using Couchbase.Tests.Utils;

namespace Couchbase.Tests
{
    [TestFixture]
    public class ConfigHelperTests
    {
        /// <summary>
        /// @test: Reads the information about available data buckets from pools,
        /// generates a random key-value, stores in the available data bucket, and then
        /// retrive the value by passing the same key for verification
        /// @pre: Add section named "pools-config" in App.config file,
        /// configure all the parameters required to initialize Couchbase client like Uri, bucket, etc.
        /// mention uri = http://Url_Of_Couchbase_Server/pools
        /// @post: Test passes if successfully stores key-value and then able to get value, fails otherwise
        /// </summary>
        [Test]
        public void Client_Operations_Succeed_When_Bootstrapping_To_Pools_Root_Uri()
        {
            var config = ConfigSectionUtils.GetConfigSection<CouchbaseClientSection>("pools-config");
            using (var client = new CouchbaseClient(config))
            {
                string key = TestUtils.GetUniqueKey(), value = TestUtils.GetRandomString();
                var storeResult = client.ExecuteStore(StoreMode.Add, key, value);
                TestUtils.StoreAssertPass(storeResult);
                var getResult = client.ExecuteGet(key);
                TestUtils.GetAssertPass(getResult, value);
            }
        }

        /// <summary>
        /// @test: Reads the information about available data buckets from pools/default,
        /// generates a random key-value, stores in the default data bucket, and then
        /// retrive the value by passing the same key for verification
        /// @pre: Add section named "pools-default-config" in App.config file,
        /// configure all the parameters required to initialize Couchbase client like Uri, bucket, etc.
        /// mention uri = http://Url_Of_Couchbase_Server/pools/default
        /// @post: Test passes if successfully stores key-value and then able to get value, fails otherwise
        /// </summary>
        [Test]
        public void Client_Operations_Succeed_When_Bootstrapping_To_Pools_Default_Root_Uri()
        {
            var config = ConfigSectionUtils.GetConfigSection<CouchbaseClientSection>("pools-default-config");
            using (var client = new CouchbaseClient(config))
            {
                string key = TestUtils.GetUniqueKey(), value = TestUtils.GetRandomString();
                var storeResult = client.ExecuteStore(StoreMode.Add, key, value);
                Assert.That(storeResult.Success, Is.True, "Success was false");
                Assert.That(storeResult.Message, Is.Null.Or.Empty, "Message was not empty");

                var getResult = client.ExecuteGet(key);
                TestUtils.GetAssertPass(getResult, value);
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