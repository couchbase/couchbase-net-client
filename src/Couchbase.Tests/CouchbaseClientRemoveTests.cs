using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

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
			var key = GetUniqueKey("remove");
			var storeResult = Store(key: key);
			StoreAssertPass(storeResult);

			var removeResult = _Client.ExecuteRemove(key);
			Assert.That(removeResult.Success, Is.True, "Success was false");
			Assert.That(removeResult.StatusCode, Is.Null.Or.EqualTo(0), "StatusCode was neither null nor 0");

			var getResult = _Client.ExecuteGet(key);
			GetAssertFail(getResult);
		}

        /// <summary>
        /// @test: Deleting a key which does not exist will fail the Remove operation
        /// @pre: Default configuration to initialize client in app.config 
        /// @post: Test passes if Remove operation fails
        /// </summary>
        [Test]
		public void When_Removing_An_Invalid_Key_Result_Is_Not_Successful()
		{
			var key = GetUniqueKey("remove");

			var removeResult = _Client.ExecuteRemove(key);
			Assert.That(removeResult.Success, Is.False, "Success was true");
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