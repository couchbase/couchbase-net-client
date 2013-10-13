﻿using System;
using NUnit.Framework;
using Enyim.Caching.Memcached;
using Couchbase.Tests.Utils;
using System.Threading;
using Enyim.Caching.Memcached.Results.StatusCodes;
using Couchbase.Protocol;

namespace Couchbase.Tests
{
	[TestFixture]
	public class CouchbaseClientGetWithLockTests : CouchbaseClientTestsBase
	{
		[Test]
		public void When_Getting_Key_With_Lock_Set_Fails_While_Locked()
		{
			var kv = KeyValueUtils.GenerateKeyAndValue("getl");

			var storeResult = Store(StoreMode.Set, kv.Item1, kv.Item2);
			StoreAssertPass(storeResult);

			var casResult = _Client.GetWithLock(kv.Item1);
			Assert.That(casResult.Result, Is.EqualTo(kv.Item2));

			storeResult = _Client.ExecuteStore(StoreMode.Set, kv.Item1, KeyValueUtils.GenerateValue());
			Assert.That(storeResult.Success, Is.False);
			Assert.That(storeResult.StatusCode.Value, Is.EqualTo((int)StatusCode.KeyExists));
		}

		[Test]
		public void When_Getting_Key_With_Lock_And_Expiry_Set_Fails_Until_Lock_Expires()
		{
			var kv = KeyValueUtils.GenerateKeyAndValue("getl");

			var storeResult = Store(StoreMode.Set, kv.Item1, kv.Item2);
			StoreAssertPass(storeResult);

			var casResult = _Client.GetWithLock(kv.Item1, TimeSpan.FromSeconds(2));
			Assert.That(casResult.Result, Is.EqualTo(kv.Item2));

			storeResult = _Client.ExecuteStore(StoreMode.Set, kv.Item1, KeyValueUtils.GenerateValue());
			Assert.That(storeResult.Success, Is.False);

			Thread.Sleep(3000);

			var newValue = KeyValueUtils.GenerateValue();
			storeResult = _Client.ExecuteStore(StoreMode.Set, kv.Item1, newValue);
			Assert.That(storeResult.Success, Is.True);

			var getResult = _Client.ExecuteGet(kv.Item1);
			GetAssertPass(getResult, newValue);
		}

		[Test]
		public void When_Getting_Key_With_Lock_And_Expiry_Set_Succeeds_With_Valid_Cas()
		{
			var kv = KeyValueUtils.GenerateKeyAndValue("getl");

			var storeResult = Store(StoreMode.Set, kv.Item1, kv.Item2);
			StoreAssertPass(storeResult);

			var casResult = _Client.GetWithLock(kv.Item1, TimeSpan.FromSeconds(15));
			Assert.That(casResult.Result, Is.EqualTo(kv.Item2));

			storeResult = _Client.ExecuteStore(StoreMode.Set, kv.Item1, KeyValueUtils.GenerateValue());
			Assert.That(storeResult.Success, Is.False);

			storeResult = _Client.ExecuteCas(StoreMode.Set, kv.Item1, KeyValueUtils.GenerateValue(), casResult.Cas);
			Assert.That(storeResult.Success, Is.True);
		}

		[Test]
		public void When_Execute_Getting_Key_With_Lock_Set_Fails_While_Locked()
		{
			var kv = KeyValueUtils.GenerateKeyAndValue("getl");

			var storeResult = Store(StoreMode.Set, kv.Item1, kv.Item2);
			StoreAssertPass(storeResult);

			var getlResult = _Client.ExecuteGetWithLock(kv.Item1);
			Assert.That(getlResult.Value, Is.EqualTo(kv.Item2));

			storeResult = _Client.ExecuteStore(StoreMode.Set, kv.Item1, KeyValueUtils.GenerateValue());
			Assert.That(storeResult.Success, Is.False);
			Assert.That(storeResult.StatusCode.Value, Is.EqualTo((int)StatusCode.KeyExists));
		}

		[Test]
		public void When_Execute_Getting_Key_With_Lock_And_Expiry_Set_Fails_Until_Lock_Expires()
		{
			var kv = KeyValueUtils.GenerateKeyAndValue("getl");

			var storeResult = Store(StoreMode.Set, kv.Item1, kv.Item2);
			StoreAssertPass(storeResult);

			var getlResult = _Client.ExecuteGetWithLock(kv.Item1, TimeSpan.FromSeconds(2));
			Assert.That(getlResult.Value, Is.EqualTo(kv.Item2));

			storeResult = _Client.ExecuteStore(StoreMode.Set, kv.Item1, KeyValueUtils.GenerateValue());
			Assert.That(storeResult.Success, Is.False);

			Thread.Sleep(3000);

			var newValue = KeyValueUtils.GenerateValue();
			storeResult = _Client.ExecuteStore(StoreMode.Set, kv.Item1, newValue);
			Assert.That(storeResult.Success, Is.True);

			var getResult = _Client.ExecuteGet(kv.Item1);
			GetAssertPass(getResult, newValue);
		}

		[Test]
		public void When_Execute_Getting_Key_With_Lock_And_Expiry_Set_Succeeds_With_Valid_Cas()
		{
			var kv = KeyValueUtils.GenerateKeyAndValue("getl");

			var storeResult = Store(StoreMode.Set, kv.Item1, kv.Item2);
			StoreAssertPass(storeResult);

			var getlResult = _Client.ExecuteGetWithLock(kv.Item1, TimeSpan.FromSeconds(15));
			Assert.That(getlResult.Value, Is.EqualTo(kv.Item2));

			storeResult = _Client.ExecuteStore(StoreMode.Set, kv.Item1, KeyValueUtils.GenerateValue());
			Assert.That(storeResult.Success, Is.False);

			storeResult = _Client.ExecuteCas(StoreMode.Set, kv.Item1, KeyValueUtils.GenerateValue(), getlResult.Cas);
			Assert.That(storeResult.Success, Is.True);
		}

		[Test]
		public void When_Generic_Execute_Getting_Key_With_Lock_Set_Fails_While_Locked()
		{
			var kv = KeyValueUtils.GenerateKeyAndValue("getl");

			var storeResult = Store(StoreMode.Set, kv.Item1, kv.Item2);
			StoreAssertPass(storeResult);

			var getlResult = _Client.ExecuteGetWithLock<string>(kv.Item1);
			Assert.That(getlResult.Value, Is.EqualTo(kv.Item2));

			storeResult = _Client.ExecuteStore(StoreMode.Set, kv.Item1, KeyValueUtils.GenerateValue());
			Assert.That(storeResult.Success, Is.False);
            Assert.That(storeResult.StatusCode.Value, Is.EqualTo((int)StatusCode.KeyExists));
		}

		[Test]
		public void When_Generic_Execute_Getting_Key_With_Lock_And_Expiry_Set_Fails_Until_Lock_Expires()
		{
			var kv = KeyValueUtils.GenerateKeyAndValue("getl");

			var storeResult = Store(StoreMode.Set, kv.Item1, kv.Item2);
			StoreAssertPass(storeResult);

			var getlResult = _Client.ExecuteGetWithLock<string>(kv.Item1, TimeSpan.FromSeconds(2));
			Assert.That(getlResult.Value, Is.EqualTo(kv.Item2));

			storeResult = _Client.ExecuteStore(StoreMode.Set, kv.Item1, KeyValueUtils.GenerateValue());
			Assert.That(storeResult.Success, Is.False);

			Thread.Sleep(3000);

			var newValue = KeyValueUtils.GenerateValue();
			storeResult = _Client.ExecuteStore(StoreMode.Set, kv.Item1, newValue);
			Assert.That(storeResult.Success, Is.True);

			var getResult = _Client.ExecuteGet(kv.Item1);
			GetAssertPass(getResult, newValue);
		}

		[Test]
		public void When_Generic_Execute_Getting_Key_With_Lock_And_Expiry_Set_Succeeds_With_Valid_Cas()
		{
			var kv = KeyValueUtils.GenerateKeyAndValue("getl");

			var storeResult = Store(StoreMode.Set, kv.Item1, kv.Item2);
			StoreAssertPass(storeResult);

			var getlResult = _Client.ExecuteGetWithLock<string>(kv.Item1, TimeSpan.FromSeconds(15));
			Assert.That(getlResult.Value, Is.EqualTo(kv.Item2));

			storeResult = _Client.ExecuteStore(StoreMode.Set, kv.Item1, KeyValueUtils.GenerateValue());
			Assert.That(storeResult.Success, Is.False);

			storeResult = _Client.ExecuteCas(StoreMode.Set, kv.Item1, KeyValueUtils.GenerateValue(), getlResult.Cas);
			Assert.That(storeResult.Success, Is.True);
		}

		[Test]
		public void When_Execute_Unlocking_A_Key_With_Valid_Cas_Key_Is_Unlocked()
		{
			var kv = KeyValueUtils.GenerateKeyAndValue("getl");

			var storeResult = Store(StoreMode.Set, kv.Item1, kv.Item2);
			StoreAssertPass(storeResult);

			var getlResult1 = _Client.ExecuteGetWithLock<string>(kv.Item1, TimeSpan.FromSeconds(15));
			Assert.That(getlResult1.Value, Is.EqualTo(kv.Item2));
			Assert.That(getlResult1.Success, Is.True);

			var getlResult2 = _Client.ExecuteGetWithLock<string>(kv.Item1, TimeSpan.FromSeconds(15));
			Assert.That(getlResult2.Success, Is.False);

			var unlockResult = _Client.ExecuteUnlock(kv.Item1, getlResult1.Cas);
			Assert.That(unlockResult.Success, Is.True);

			storeResult = Store(StoreMode.Set, kv.Item1, kv.Item2);
			StoreAssertPass(storeResult);

			var getlResult3 = _Client.ExecuteGetWithLock<string>(kv.Item1, TimeSpan.FromSeconds(15));
			Assert.That(getlResult3.Success, Is.True);

		}

		[Test]
		public void When_Execute_Unlocking_A_Key_With_Invalid_Cas_Key_Is_Unlocked()
		{
			var kv = KeyValueUtils.GenerateKeyAndValue("getl");

			var storeResult = Store(StoreMode.Set, kv.Item1, kv.Item2);
			StoreAssertPass(storeResult);

			var getlResult1 = _Client.ExecuteGetWithLock<string>(kv.Item1, TimeSpan.FromSeconds(15));
			Assert.That(getlResult1.Value, Is.EqualTo(kv.Item2));
			Assert.That(getlResult1.Success, Is.True);

			var getlResult2 = _Client.ExecuteGetWithLock<string>(kv.Item1, TimeSpan.FromSeconds(15));
			Assert.That(getlResult2.Success, Is.False);

			var unlockResult = _Client.ExecuteUnlock(kv.Item1, getlResult1.Cas-1);
			Assert.That(unlockResult.Success, Is.False);

			storeResult = Store(StoreMode.Set, kv.Item1, kv.Item2);
			StoreAssertFail(storeResult);

			var getlResult3 = _Client.ExecuteGetWithLock<string>(kv.Item1, TimeSpan.FromSeconds(15));
			Assert.That(getlResult3.Success, Is.False);

		}

		[Test]
		public void When_Execute_Getting_Key_With_Lock_And_Expiry_Second_Lock_Attempt_Does_Not_Change_Expiry()
		{
			var kv = KeyValueUtils.GenerateKeyAndValue("getl");

			var storeResult = Store(StoreMode.Set, kv.Item1, kv.Item2);
			StoreAssertPass(storeResult);

			var getlResult = _Client.ExecuteGetWithLock(kv.Item1, TimeSpan.FromSeconds(6));
			Assert.That(getlResult.Value, Is.EqualTo(kv.Item2));

			var otherGetlResult = _Client.ExecuteGetWithLock(kv.Item1, TimeSpan.FromSeconds(2));
			Assert.That(otherGetlResult.Cas, Is.Not.EqualTo(getlResult.Cas));
			Assert.That(otherGetlResult.StatusCode, Is.EqualTo((int)CouchbaseStatusCodeEnums.LockError));

			Thread.Sleep(3000);

			storeResult = _Client.ExecuteStore(StoreMode.Set, kv.Item1, KeyValueUtils.GenerateValue());
			Assert.That(storeResult.Success, Is.False);
            Assert.That(storeResult.StatusCode.Value, Is.EqualTo((int)StatusCode.KeyExists));

			Thread.Sleep(4000);

			var newValue = KeyValueUtils.GenerateValue();
			storeResult = _Client.ExecuteStore(StoreMode.Set, kv.Item1, newValue);
			Assert.That(storeResult.Success, Is.True, "Failed to update item");

			var getResult = _Client.ExecuteGet(kv.Item1);
			GetAssertPass(getResult, newValue);
		}

		[Test]
		[ExpectedException(typeof(ArgumentOutOfRangeException))]
		public void When_Timeout_Is_Set_To_Greater_Than_30_Seconds_Exception_Is_Thrown()
		{
			var kv = KeyValueUtils.GenerateKeyAndValue("getl");

			var storeResult = Store(StoreMode.Set, kv.Item1, kv.Item2);
			StoreAssertPass(storeResult);

			var getlResult = _Client.ExecuteGetWithLock(kv.Item1, TimeSpan.FromSeconds(31));
		}

		[Test]
		[ExpectedException(typeof(ArgumentOutOfRangeException))]
		public void When_Timeout_Is_Less_Than_Or_Equal_To_30_Seconds_Exception_Is_Not_Thrown()
		{
			var kv = KeyValueUtils.GenerateKeyAndValue("getl");

			var storeResult = Store(StoreMode.Set, kv.Item1, kv.Item2);
			StoreAssertPass(storeResult);

			_Client.ExecuteGetWithLock(kv.Item1, TimeSpan.FromSeconds(30));
			_Client.ExecuteGetWithLock(kv.Item1, TimeSpan.FromSeconds(29));
			Assert.Pass();
		}
	}
}

/**
 * Copyright (c) 2013 Couchbase, Inc. All rights reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
 * except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software distributed under the
 * License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
 * either express or implied. See the License for the specific language governing permissions
 * and limitations under the License.
 */