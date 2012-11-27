using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Enyim.Caching.Memcached;
using Couchbase.Operations;
using Couchbase.Operations.Constants;
using Couchbase.Tests.Utils;
using Couchbase.Tests.Factories;
using System.IO;
using System.Threading;

namespace Couchbase.Tests
{
	[TestFixture]
	public class CouchbaseClientObserveTests : CouchbaseClientTestsBase
	{

		[Test]
		public void When_Storing_A_New_Key_Observe_Will_Succeed_With_Master_Only_Persistence()
		{
			var kv = KeyValueUtils.GenerateKeyAndValue("observe");
			var storeResult = _Client.ExecuteStore(StoreMode.Set, kv.Item1, kv.Item2, PersistTo.One);
			StoreAssertPass(storeResult);
		}

		[Test]
		public void When_Storing_A_New_Key_Observe_Will_Succeed_With_Master_Persistence_And_Single_Node_Replication()
		{
			var kv = KeyValueUtils.GenerateKeyAndValue("observe");
			var storeResult = _Client.ExecuteStore(StoreMode.Set, kv.Item1, kv.Item2, PersistTo.One, ReplicateTo.One);
			StoreAssertPass(storeResult);
		}

		[Test]
		public void When_Storing_A_New_Key_Observe_Will_Succeed_With_Master_Persistence_And_Mutli_Node_Replication()
		{
			var kv = KeyValueUtils.GenerateKeyAndValue("observe");
			var storeResult = _Client.ExecuteStore(StoreMode.Set, kv.Item1, kv.Item2, PersistTo.One, ReplicateTo.Two);
			StoreAssertPass(storeResult);
		}

		[Test]
		public void When_Storing_A_New_Key_Observe_Will_Succeed_With_Multi_Node_Persistence()
		{
			var kv = KeyValueUtils.GenerateKeyAndValue("observe");
			var storeResult = _Client.ExecuteStore(StoreMode.Set, kv.Item1, kv.Item2, PersistTo.Two);
			StoreAssertPass(storeResult);
		}

		[Test]
		public void When_Storing_A_New_Key_Observe_Will_Succeed_With_Multi_Node_Persistence_And_Single_Node_Replication()
		{
			var kv = KeyValueUtils.GenerateKeyAndValue("observe");
			var storeResult = _Client.ExecuteStore(StoreMode.Set, kv.Item1, kv.Item2, PersistTo.Two, ReplicateTo.One);
			StoreAssertPass(storeResult);
		}

		[Test]
		public void When_Storing_A_New_Key_Observe_Will_Succeed_With_Single_Node_Replication()
		{
			var kv = KeyValueUtils.GenerateKeyAndValue("observe");
			var storeResult = _Client.ExecuteStore(StoreMode.Set, kv.Item1, kv.Item2, ReplicateTo.One);
			StoreAssertPass(storeResult);
		}

		[Test]
		public void When_Storing_A_New_Key_Observe_Will_Succeed_With_Multi_Node_Replication()
		{
			var kv = KeyValueUtils.GenerateKeyAndValue("observe");
			var storeResult = _Client.ExecuteStore(StoreMode.Set, kv.Item1, kv.Item2, ReplicateTo.One);
			StoreAssertPass(storeResult);
		}

		[Test]
		public void When_Storing_A_New_Key_Observe_Will_Fail_When_Cluster_Has_Too_Few_Nodes_For_Replication()
		{
			var kv = KeyValueUtils.GenerateKeyAndValue("observe");
			var storeResult = _Client.ExecuteStore(StoreMode.Set, kv.Item1, kv.Item2, ReplicateTo.Three);
			Assert.That(storeResult.Success, Is.False);
		}

		[Test]
		public void When_Storing_A_New_Key_Observe_Will_Fail_With_Master_Persistence_And_Cas_Value_Has_Changed()
		{
			var kv = KeyValueUtils.GenerateKeyAndValue("observe");
			var storeResult = _Client.ExecuteStore(StoreMode.Set, kv.Item1, kv.Item2);
			var observeResult = _Client.Observe(kv.Item2, storeResult.Cas - 1, PersistTo.One, ReplicateTo.Zero);
			Assert.That(observeResult.Success, Is.False);
			Assert.That(observeResult.Message, Is.StringMatching(ObserveOperationConstants.MESSAGE_MODIFIED));
		}

		[Test]
		public void When_Storing_A_New_Key_Observe_Will_Fail_With_Master_Persistence_And_Replication_And_Cas_Value_Has_Changed()
		{
			var kv = KeyValueUtils.GenerateKeyAndValue("observe");
			var storeResult = _Client.ExecuteStore(StoreMode.Set, kv.Item1, kv.Item2);
			var observeResult = _Client.Observe(kv.Item2, storeResult.Cas - 1, PersistTo.One, ReplicateTo.Two);
			Assert.That(observeResult.Success, Is.False);
			Assert.That(observeResult.Message, Is.StringMatching(ObserveOperationConstants.MESSAGE_MODIFIED));
		}

		[Test]
		public void When_Storing_A_New_Key_With_Master_Persistence_That_Key_Is_In_View_When_Stale_Is_False()
		{
			var cluster = CouchbaseClusterFactory.CreateCouchbaseCluster();
			var docResult = cluster.CreateDesignDocument("default", "cities", new FileStream("Data\\CityViews.json", FileMode.Open));
			Assert.That(docResult, Is.True, "Create design doc failed");

			var key = "city_Waterbury_CT";
			var value = "{ \"name\" : \"Waterbury\", \"state\" : \"CT\", \"type\" : \"city\" }";
			var storeResult = _Client.ExecuteStore(StoreMode.Set, key, value, PersistTo.One);
			Assert.That(storeResult.Success, Is.True);

			var view = _Client.GetView("cities", "by_id").Key(key);


			int i = 0;
			foreach (var item in view)
			{
				i++;
				Assert.That(item.ItemId, Is.StringMatching(key));
				break;
			}

			Assert.That(i, Is.EqualTo(1));

			var deleteResult = _Client.Remove(key);
			Assert.That(deleteResult, Is.True);

			var deleteDesignDocResult = cluster.DeleteDesignDocument("default", "cities");
			Assert.That(deleteDesignDocResult, Is.True);
		}

		[Test]
		public void When_Observing_A_Removed_Key_Operation_Is_Successful_With_Master_Node_Persistence()
		{
			var key = GetUniqueKey("observe");
			var value = GetRandomString();

			var storeResult = Store(StoreMode.Set, key, value);
			StoreAssertPass(storeResult);

			var removeResult = _Client.ExecuteRemove(key, PersistTo.One);
			Assert.That(removeResult.Success, Is.True);

			var getResult = _Client.ExecuteGet(key);
			GetAssertFail(getResult);
		}

		[Test]
		public void When_Observing_A_Removed_Key_Operation_Is_Successful_With_Master_And_Replication_Persistence()
		{
			var key = GetUniqueKey("observe");
			var value = GetRandomString();

			var storeResult = _Client.ExecuteStore(StoreMode.Set, key, PersistTo.One, ReplicateTo.Two);
			StoreAssertPass(storeResult);

			var removeResult = _Client.ExecuteRemove(key, PersistTo.One, ReplicateTo.Two);
			Assert.That(removeResult.Success, Is.True);

			var getResult = _Client.ExecuteGet(key);
			GetAssertFail(getResult);
		}

		[Test]
		public void When_Observing_A_Removed_Key_Operation_Is_Successful_With_Replication_Only()
		{
			var key = GetUniqueKey("observe");
			var value = GetRandomString();

			var storeResult = _Client.ExecuteStore(StoreMode.Set, key, PersistTo.One, ReplicateTo.Two);
			StoreAssertPass(storeResult);

			var removeResult = _Client.ExecuteRemove(key, PersistTo.Zero, ReplicateTo.Two);
			Assert.That(removeResult.Success, Is.True);

			var getResult = _Client.ExecuteGet(key);
			GetAssertFail(getResult);
		}

		[Test]
		public void When_Observing_A_Removed_Key_Operation_Is_Successful_With_Multi_Node_Persistence()
		{
			var key = GetUniqueKey("observe");
			var value = GetRandomString();

			var storeResult = _Client.ExecuteStore(StoreMode.Set, key, PersistTo.One, ReplicateTo.Two);
			StoreAssertPass(storeResult);

			var removeResult = _Client.ExecuteRemove(key, PersistTo.Two);
			Assert.That(removeResult.Success, Is.True);

			var getResult = _Client.ExecuteGet(key);
			GetAssertFail(getResult);
		}
	}
}

#region [ License information		  ]
/* ************************************************************
 * 
 *	@author Couchbase <info@couchbase.com>
 *	@copyright 2012 Couchbase, Inc.
 *	
 *	Licensed under the Apache License, Version 2.0 (the "License");
 *	you may not use this file except in compliance with the License.
 *	You may obtain a copy of the License at
 *	
 *		http://www.apache.org/licenses/LICENSE-2.0
 *	
 *	Unless required by applicable law or agreed to in writing, software
 *	distributed under the License is distributed on an "AS IS" BASIS,
 *	WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *	See the License for the specific language governing permissions and
 *	limitations under the License.
 *	
 * ************************************************************/
#endregion