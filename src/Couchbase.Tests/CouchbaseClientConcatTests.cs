using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Couchbase.Tests
{
	[TestFixture]
	public class CouchbaseClientConcatTests : CouchbaseClientTestsBase
	{
        /// <summary>
        /// @test: Store a randomly generated unique key-value, use the different 
        /// cas value in ExecuteCas() mehod than what is returned by Store() method, 
        /// the ExecuteCas() fails due to invalid cas passed
        /// @pre: No section required in App.config file
        /// @post: Test passes if ExecuteCas() fails due to invalid cas, fails if ExecuteCas() passes
        /// </summary>
		[Test]
		public void When_Appending_To_Existing_Value_Result_Is_Successful()
		{
			var key = GetUniqueKey("concat");
			var value = GetRandomString();

			var storeResult = Store(key: key);
			StoreAssertPass(storeResult);

			var toAppend = "The End";
			var data = new ArraySegment<byte>(Encoding.ASCII.GetBytes(toAppend));
			var concatResult = _Client.ExecuteAppend(key, data);
			ConcatAssertPass(concatResult);

			var getResult = _Client.ExecuteGet(key);
			GetAssertPass(getResult, value + toAppend);
		}


        /// <summary>
        /// @test: Generate a unique key, use ExecuteAppend() to save data in that key,
        /// since the key does not exist aleady, append operation would fail, then get value from
        /// the same key, it will fail again as the key does not exist
        /// @pre: Generate a unique key and data to append in that key
        /// @post: Test passes if ExecuteAppend() and ExecuteGet() both fail, which is the expected behaviour
        /// </summary>
		[Test]
		public void When_Appending_To_Invalid_Key_Result_Is_Not_Successful()
		{
			var key = GetUniqueKey("concat");

			var toAppend = "The End";
			var data = new ArraySegment<byte>(Encoding.ASCII.GetBytes(toAppend));
			var concatResult = _Client.ExecuteAppend(key, data);
			ConcatAssertFail(concatResult);

			var getResult = _Client.ExecuteGet(key);
			GetAssertFail(getResult);

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
			var key = GetUniqueKey("concat");
			var value = GetRandomString();

			var storeResult = Store(key: key);
			StoreAssertPass(storeResult);

			var toPrepend = "The Beginning";
			var data = new ArraySegment<byte>(Encoding.ASCII.GetBytes(toPrepend));
			var concatResult = _Client.ExecutePrepend(key, data);
			ConcatAssertPass(concatResult);

			var getResult = _Client.ExecuteGet(key);
			GetAssertPass(getResult, toPrepend + value);

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
			var key = GetUniqueKey("concat");

			var toPrepend = "The End";
			var data = new ArraySegment<byte>(Encoding.ASCII.GetBytes(toPrepend));
			var concatResult = _Client.ExecutePrepend(key, data);
			ConcatAssertFail(concatResult);

			var getResult = _Client.ExecuteGet(key);
			GetAssertFail(getResult);

		}

        /// <summary>
        /// @test: Generate a unique key and store it, Prepend some text to that key-value, 
        /// it should pass, then get the value against the same key, it should pass
        /// @pre: Generate a unique key and data to append in that key
        /// @post: Test passes if ExecutePrepend() and ExecuteGet() both pass
        /// </summary>
		[Test]
		public void When_Appending_To_Existing_Value_Result_Is_Successful_With_Valid_Cas()
		{
			var key = GetUniqueKey("concat");
			var value = GetRandomString();

			var storeResult = Store(key: key);
			StoreAssertPass(storeResult);

			var toAppend = "The End";
			var data = new ArraySegment<byte>(Encoding.ASCII.GetBytes(toAppend));
			var concatResult = _Client.ExecuteAppend(key, storeResult.Cas, data);
			ConcatAssertPass(concatResult);

			var getResult = _Client.ExecuteGet(key);
			GetAssertPass(getResult, value + toAppend);

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
			var key = GetUniqueKey("concat");
			var value = GetRandomString();

			var storeResult = Store(key: key);
			StoreAssertPass(storeResult);

			var toAppend = "The End";
			var data = new ArraySegment<byte>(Encoding.ASCII.GetBytes(toAppend));
			var concatResult = _Client.ExecuteAppend(key, storeResult.Cas - 1, data);
			ConcatAssertFail(concatResult);
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
			var key = GetUniqueKey("concat");
			var value = GetRandomString();

			var storeResult = Store(key: key);
			StoreAssertPass(storeResult);

			var tpPrepend = "The Beginning";
			var data = new ArraySegment<byte>(Encoding.ASCII.GetBytes(tpPrepend));
			var concatResult = _Client.ExecuteAppend(key, storeResult.Cas, data);
			ConcatAssertPass(concatResult);

			var getResult = _Client.ExecuteGet(key);
			GetAssertPass(getResult, value + tpPrepend);
		}

        /// <summary>
        /// @test: Generate a unique key and store it, Append some text and use the different cas key,
        /// the test should fail
        /// @pre: Generate a unique key and data to append in that key
        /// @post: Test passes if ExecuteAppend() passes
        /// </summary>
		[Test]
		public void When_Prepending_To_Existing_Value_Result_Is_Not_Successful_With_Invalid_Cas()
		{
			var key = GetUniqueKey("concat");
			var value = GetRandomString();

			var storeResult = Store(key: key);
			StoreAssertPass(storeResult);

			var tpPrepend = "The Beginning";
			var data = new ArraySegment<byte>(Encoding.ASCII.GetBytes(tpPrepend));
			var concatResult = _Client.ExecuteAppend(key, storeResult.Cas - 1, data);
			ConcatAssertFail(concatResult);

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