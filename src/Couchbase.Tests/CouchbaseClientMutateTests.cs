﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Couchbase.Tests
{
	[TestFixture]
	public class CouchbaseClientMutateTests : CouchbaseClientTestsBase
	{
        /// <summary>
        /// @test: Store a randomly generated key, with a value lets say 100, increment
        /// the key with the delta of lets say 10, the result of mutation should be 110
        /// @pre: Default configuration to initialize client in app.config 
        /// @post: Test passes if mutate operation increments the key correctly
        /// </summary>
		[Test]
		public void When_Incrementing_Value_Result_Is_Successful()
		{
			var key = GetUniqueKey("mutate");
			var mutateResult = _Client.ExecuteIncrement(key, 100, 10);
			MutateAssertPass(mutateResult, 100);

			mutateResult = _Client.ExecuteIncrement(key, 100, 10);
			MutateAssertPass(mutateResult, 110);
		}

        /// <summary>
        /// @test: Store a randomly generated key, with a value lets say 100, decrement
        /// the key with the delta of lets say 10, the result of mutation should be 90
        /// @pre: Default configuration to initialize client in app.config 
        /// @post: Test passes if mutate operation decrements the key correctly
        /// </summary>
		[Test]
		public void When_Decrementing_Value_Result_Is_Successful()
		{
			var key = GetUniqueKey("mutate");
			var mutateResult = _Client.ExecuteDecrement(key, 100, 10);
			MutateAssertPass(mutateResult, 100);

			mutateResult = _Client.ExecuteDecrement(key, 100, 10);
			MutateAssertPass(mutateResult, 90);
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