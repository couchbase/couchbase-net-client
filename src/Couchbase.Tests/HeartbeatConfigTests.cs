using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Couchbase.Configuration;
using Enyim.Caching.Memcached;
using Couchbase.Tests.Utils;

namespace Couchbase.Tests
{
    /*
         * The client will periodically check the health of its connection to the cluster by performing a heartbeat check. By default,
         * this test is done every 10 seconds against the bootstrap URI defined in the servers element.
         * The "uri", "enabled" and "interval" attributes are all optional. The "interval" is specified in milliseconds. Setting "enabled"
         * to false will cause other settings to be ignored and the heartbeat will not be checked.
         * <heartbeatMonitor uri="http://127.0.0.1:8091/pools/heartbeat" interval="60000" enabled="true" />
    */
	[TestFixture]
	public class HeartbeatConfigTests : CouchbaseClientTestsBase
	{
        /// <summary>
        /// @test: Reads the configuration from App.config which enables the heartbeat and then perform 
        /// client operations 
        /// @pre: Add section named "heartbeat-config-on" in App.config file, enable heartbeat at a specific time interval 
        /// @post: Test passes if with heartbeat on, the client can successfully store key-value and then able to get value; 
        /// fails otherwise
        /// </summary>
		[Test]
		public void Client_Operations_Succeed_When_Heartbeat_Is_Configured()
		{
			var config = ConfigSectionUtils.GetConfigSection<CouchbaseClientSection>("heartbeat-config-on");
			var client = new CouchbaseClient(config);

			string key = GetUniqueKey(), value = GetRandomString();
			var storeResult = client.ExecuteStore(StoreMode.Add, key, value);
			StoreAssertPass(storeResult);

			var getResult = client.ExecuteGet(key);
			GetAssertPass(getResult, value);

		}

        /// <summary>
        /// @test: Reads the configuration from App.config which disables the heartbeat and then perform 
        /// client operations 
        /// @pre: Add section named "heartbeat-config-off" in App.config file, disable heartbeat
        /// @post: Test passes if with heartbeat off, the client can successfully store key-value and then able to get value; 
        /// fails otherwise
        /// </summary>
		[Test]
		public void Client_Operations_Succeed_When_Heartbeat_Is_Disabled()
		{
			var config = ConfigSectionUtils.GetConfigSection<CouchbaseClientSection>("heartbeat-config-off");
			var client = new CouchbaseClient(config);

			string key = GetUniqueKey(), value = GetRandomString();
			var storeResult = client.ExecuteStore(StoreMode.Add, key, value);
			StoreAssertPass(storeResult);

			var getResult = client.ExecuteGet(key);
			GetAssertPass(getResult, value);

		}

        /// <summary>
        /// @test: Reads the configuration from App.config which has no information on heartbeat and then perform 
        /// client operations 
        /// @pre: Add section named "min-config" in App.config file, add information about bucket, bucket password, username/password
        /// and server uri.
        /// @post: Test passes if the client can successfully store key-value and then able to get value; fails otherwise
        /// </summary>
		[Test]
		public void Client_Operations_Succeed_When_Heartbeat_Is_Not_Configured()
		{
			var config = ConfigSectionUtils.GetConfigSection<CouchbaseClientSection>("min-config");
			var client = new CouchbaseClient(config);

			string key = GetUniqueKey(), value = GetRandomString();
			var storeResult = client.ExecuteStore(StoreMode.Add, key, value);
			StoreAssertPass(storeResult);

			var getResult = client.ExecuteGet(key);
			GetAssertPass(getResult, value);

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
