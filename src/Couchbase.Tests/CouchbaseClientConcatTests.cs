using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Couchbase.Tests.Factories;
using Couchbase.Tests.Utils;
using NUnit.Framework;

namespace Couchbase.Tests
{
	[TestFixture]
	public class CouchbaseClientConcatTests : CouchbaseClientTestsBase
	{
        /// <summary>
        /// @test: Store a randomly generated unique key-value, use ExecuteAppend() to append some string
        /// to its value and then get the new string after appending
        /// @pre: No section required in App.config file, it picks up the default configuration
        /// @post: Test passes if ExecuteAppend() passes and Get method returns the new string after appending text to it
        /// </summary>
		[Test]
		public void When_Appending_To_Existing_Value_Result_Is_Successful()
		{
			var key = TestUtils.GetUniqueKey("concat");
            var value = TestUtils.GetRandomString();

            var storeResult = TestUtils.Store(Client, key: key);
            TestUtils.StoreAssertPass(storeResult);

			var toAppend = "The End";
			var data = new ArraySegment<byte>(Encoding.ASCII.GetBytes(toAppend));
			var concatResult = Client.ExecuteAppend(key, data);
            TestUtils.ConcatAssertPass(concatResult);

			var getResult = Client.ExecuteGet(key);
            TestUtils.GetAssertPass(getResult, value + toAppend);
		}


        /// <summary>
        /// @test: Generate a unique key, use ExecuteAppend() to save data in that key,
        /// since the key does not exist already, append operation would fail, then get value from
        /// the same key, it will fail again as the key does not exist
        /// @pre: Generate a unique key and data to append in that key
        /// @post: Test passes if ExecuteAppend() and ExecuteGet() both fail, which is the expected behaviour
        /// </summary>
		[Test]
		public void When_Appending_To_Invalid_Key_Result_Is_Not_Successful()
		{
            var key = TestUtils.GetUniqueKey("concat");

			var toAppend = "The End";
			var data = new ArraySegment<byte>(Encoding.ASCII.GetBytes(toAppend));
			var concatResult = Client.ExecuteAppend(key, data);
            TestUtils.ConcatAssertFail(concatResult);

			var getResult = Client.ExecuteGet(key);
            TestUtils.GetAssertFail(getResult);

		}

        /// <summary>
        /// @test: Generate a unique key and store it, Prepend some text to that key-value, 
        /// it should pass, then get the value against the same key, it should pass
        /// @pre: Generate a unique key and data to prepend in that key
        /// @post: Test passes if ExecutePrepend() and ExecuteGet() both pass
        /// </summary>
		[Test]
		public void When_Prepending_To_Existing_Value_Result_Is_Successful()
		{
            var key = TestUtils.GetUniqueKey("concat");
            var value = TestUtils.GetRandomString();

            var storeResult = TestUtils.Store(Client, key: key);
            TestUtils.StoreAssertPass(storeResult);

			var toPrepend = "The Beginning";
			var data = new ArraySegment<byte>(Encoding.ASCII.GetBytes(toPrepend));
			var concatResult = Client.ExecutePrepend(key, data);
            TestUtils.ConcatAssertPass(concatResult);

			var getResult = Client.ExecuteGet(key);
            TestUtils.GetAssertPass(getResult, toPrepend + value);

		}

        /// <summary>
        /// @test: Generate a unique key but do not store it, Prepend some text to that key-value, 
        /// it should fail as key is invalid, then get the value against the same key, it should fail
        /// @pre: Generate a unique key and data to prepend in that key
        /// @post: Test passes if ExecutePrepend() and ExecuteGet() both fail
        /// </summary>
		[Test]
		public void When_Prepending_To_Invalid_Key_Result_Is_Not_Successful()
		{
            var key = TestUtils.GetUniqueKey("concat");

			var toPrepend = "The End";
			var data = new ArraySegment<byte>(Encoding.ASCII.GetBytes(toPrepend));
			var concatResult = Client.ExecutePrepend(key, data);
            TestUtils.ConcatAssertFail(concatResult);

			var getResult = Client.ExecuteGet(key);
            TestUtils.GetAssertFail(getResult);

		}

        /// <summary>
        /// @test: Generate a unique key and store it, test checks to see that
        /// the final output key contains both the 'original' (i.e. "concat") value as well as
        /// the appended value. (i.e. "concatTheEnd")
        /// @pre: Generate a unique key and data to append in that key
        /// @post: Test passes if ExecuteAppend() and ExecuteGet() both pass
        /// </summary>
		[Test]
		public void When_Appending_To_Existing_Value_Result_Is_Successful_With_Valid_Cas()
		{
            var key = TestUtils.GetUniqueKey("concat");
            var value = TestUtils.GetRandomString();

            var storeResult = TestUtils.Store(Client, key: key);
            TestUtils.StoreAssertPass(storeResult);

			var toAppend = "The End";
			var data = new ArraySegment<byte>(Encoding.ASCII.GetBytes(toAppend));
			var concatResult = Client.ExecuteAppend(key, storeResult.Cas, data);
            TestUtils.ConcatAssertPass(concatResult);

			var getResult = Client.ExecuteGet(key);
            TestUtils.GetAssertPass(getResult, value + toAppend);

		}

        /// <summary>
        /// @test: Generate a unique key and store it, Append some text but use a different key
        /// it should fail
        /// @pre: Generate a unique key and data to append in that key
        /// @post: Test passes if ExecuteAppend() fails
        /// </summary>
		[Test]
		public void When_Appending_To_Existing_Value_Result_Is_Not_Successful_With_Invalid_Cas()
		{
            var key = TestUtils.GetUniqueKey("concat");
            var value = TestUtils.GetRandomString();

            var storeResult = TestUtils.Store(Client, key: key);
            TestUtils.StoreAssertPass(storeResult);

			var toAppend = "The End";
			var data = new ArraySegment<byte>(Encoding.ASCII.GetBytes(toAppend));
			var concatResult = Client.ExecuteAppend(key, storeResult.Cas - 1, data);
            TestUtils.ConcatAssertFail(concatResult);
		}

        /// <summary>
        /// @test: Generate a unique key and store it, Append some text and use the same cas key
        /// then get the value against the same key
        /// @pre: Generate a unique key and data to append in that key
        /// @post: Test passes if ExecuteAppend() and ExecuteGet() passes
        /// </summary>
		[Test]
		public void When_Prepending_To_Existing_Value_Result_Is_Successful_With_Valid_Cas()
		{
            var key = TestUtils.GetUniqueKey("concat");
            var value = TestUtils.GetRandomString();

            var storeResult = TestUtils.Store(Client, key: key);
            TestUtils.StoreAssertPass(storeResult);

			var tpPrepend = "The Beginning";
			var data = new ArraySegment<byte>(Encoding.ASCII.GetBytes(tpPrepend));
			var concatResult = Client.ExecuteAppend(key, storeResult.Cas, data);
            TestUtils.ConcatAssertPass(concatResult);

			var getResult = Client.ExecuteGet(key);
            TestUtils.GetAssertPass(getResult, value + tpPrepend);
		}

        /// <summary>
        /// @test: Generate a unique key and store it, Append some text and use the different cas key,
        /// the test should fail
        /// @pre: Generate a unique key and data to append in that key
        /// @post: Test passes if ExecuteAppend() fails since the Cas is invalid
        /// </summary>
		[Test]
		public void When_Prepending_To_Existing_Value_Result_Is_Not_Successful_With_Invalid_Cas()
		{
            var key = TestUtils.GetUniqueKey("concat");
            var value = TestUtils.GetRandomString();

            var storeResult = TestUtils.Store(Client, key: key);
            TestUtils.StoreAssertPass(storeResult);

			var tpPrepend = "The Beginning";
			var data = new ArraySegment<byte>(Encoding.ASCII.GetBytes(tpPrepend));
			var concatResult = Client.ExecuteAppend(key, storeResult.Cas - 1, data);
            TestUtils.ConcatAssertFail(concatResult);
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