using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Enyim.Caching.Memcached;
using Couchbase.Operations;
using Couchbase.Operations.Constants;

namespace Couchbase.Tests
{
	[TestFixture]
	public class CouchbaseClientObserveTests : CouchbaseClientTestsBase
	{
		[Test]
		public void When_Storing_A_New_Key_Observe_Will_Succeed_When_Persist_Is_One_And_Replicate_Is_Default_Cas_Is_Same()
		{
			var key = GetUniqueKey("store");
			var value = GetRandomString();
			var storeResult = _Client.ExecuteStore(StoreMode.Set, key, value, PersistTo.One);
			StoreAssertPass(storeResult);
		}

		[Test]
		public void When_Storing_A_New_Key_Observe_Will_Succeed_When_Persist_Is_One_And_Cas_Is_Same()
		{
			var key = GetUniqueKey("store");
			var value = GetRandomString();
			var storeResult = _Client.ExecuteStore(StoreMode.Set, key, value);
			StoreAssertPass(storeResult);

			var observeResult = _Client.Observe(key, storeResult.Cas, PersistTo.One, ReplicateTo.Zero);
			Assert.That(observeResult.Success, Is.True, observeResult.Message);
		}

		[Test]
		public void When_Storing_A_New_Key_Observe_Will_Fail_When_Persist_Is_One_And_Cas_Is_Different()
		{
			var key = GetUniqueKey("store");
			var value = GetRandomString();
			var storeResult = _Client.ExecuteStore(StoreMode.Set, key, value);
			StoreAssertPass(storeResult);

			var observeResult = _Client.Observe(key, storeResult.Cas - 1, PersistTo.One, ReplicateTo.Zero);
			Assert.That(observeResult.Success, Is.Not.True);
			Assert.That(observeResult.Message, Is.StringMatching(ObserveOperationConstants.MESSAGE_MODIFIED));

		}

		[Test]
		public void When_Storing_A_New_Key_Observe_Will_Succeed_When_Persist_Is_Two_And_Cas_Is_Same()
		{
			var key = GetUniqueKey("store");
			var value = GetRandomString();
			var storeResult = _Client.ExecuteStore(StoreMode.Set, key, value);
			StoreAssertPass(storeResult);

			var observeResult = _Client.Observe(key, storeResult.Cas, PersistTo.Two, ReplicateTo.Zero);
			Assert.That(observeResult.Success, Is.True, observeResult.Message);
		}

		[Test]
		public void When_Storing_A_New_Key_Observe_Will_Fail_When_Persist_Is_Two_And_Cas_Is_Different()
		{
			var key = GetUniqueKey("store");
			var value = GetRandomString();
			var storeResult = _Client.ExecuteStore(StoreMode.Set, key, value);
			StoreAssertPass(storeResult);

			var observeResult = _Client.Observe(key, storeResult.Cas - 1, PersistTo.Two, ReplicateTo.Zero);
			Assert.That(observeResult.Success, Is.Not.True);
			Assert.That(observeResult.Message, Is.StringMatching(ObserveOperationConstants.MESSAGE_MODIFIED));

		}

		[Test]
		public void When_Storing_A_New_Key_Observe_Will_Succeed_When_Persist_Is_Two_And_Replicate_Is_Two_And_Cas_Is_Same()
		{
			var key = GetUniqueKey("store");
			var value = GetRandomString();
			var storeResult = _Client.ExecuteStore(StoreMode.Set, key, value);
			StoreAssertPass(storeResult);

			var observeResult = _Client.Observe(key, storeResult.Cas, PersistTo.Two, ReplicateTo.Two);
			Assert.That(observeResult.Success, Is.True, observeResult.Message);
		}

		[Test]
		public void When_Storing_A_New_Key_Observe_Will_Fail_When_Persist_Is_Two_And_Replicate_Is_Two_And_Cas_Is_Different()
		{
			var key = GetUniqueKey("store");
			var value = GetRandomString();
			var storeResult = _Client.ExecuteStore(StoreMode.Set, key, value);
			StoreAssertPass(storeResult);

			var observeResult = _Client.Observe(key, storeResult.Cas - 1, PersistTo.Two, ReplicateTo.Two);
			Assert.That(observeResult.Success, Is.Not.True);
			Assert.That(observeResult.Message, Is.StringMatching(ObserveOperationConstants.MESSAGE_MODIFIED));

		}

		[Test]
		public void When_Storing_A_New_Key_Observe_Will_Succeed_When_Persist_Is_Zero_And_Replicate_Is_Two_And_Cas_Is_Same()
		{
			var key = GetUniqueKey("store");
			var value = GetRandomString();
			var storeResult = _Client.ExecuteStore(StoreMode.Set, key, value);
			StoreAssertPass(storeResult);

			var observeResult = _Client.Observe(key, storeResult.Cas, PersistTo.Two, ReplicateTo.Two);
			Assert.That(observeResult.Success, Is.True, observeResult.Message);
		}

		[Test]
		public void When_Storing_A_New_Key_Observe_Will_Fail_When_Persist_Is_Zero_And_Replicate_Is_Two_And_Cas_Is_Different()
		{
			var key = GetUniqueKey("store");
			var value = GetRandomString();
			var storeResult = _Client.ExecuteStore(StoreMode.Set, key, value);
			StoreAssertPass(storeResult);

			var observeResult = _Client.Observe(key, storeResult.Cas - 1, PersistTo.Two, ReplicateTo.Two);
			Assert.That(observeResult.Success, Is.Not.True);
			Assert.That(observeResult.Message, Is.StringMatching(ObserveOperationConstants.MESSAGE_MODIFIED));

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