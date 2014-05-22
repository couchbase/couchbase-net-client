using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Enyim.Caching.Memcached;
using Enyim.Caching.Memcached.Results;
using NUnit.Framework;

namespace Couchbase.Tests.Utils
{
    public static class TestUtils
    {
        public static IStoreOperationResult Store(ICouchbaseClient client, StoreMode mode = StoreMode.Set, string key = null, string value = null)
        {
            if (string.IsNullOrEmpty(key))
            {
                key = GetUniqueKey("store");
            }

            if (value == null)
            {
                value = GetRandomString();
            }
            return client.ExecuteStore(mode, key, value);
        }

        /// <summary>
        /// Store a document with a TTL (time to live) value.
        /// </summary>
        /// <param name="ts">TimeSpan, e.g. TimeSpan.FromDays(60) stores document for 60-days</param>
        /// <returns></returns>
        public static IStoreOperationResult Store(ICouchbaseClient client, TimeSpan ts, StoreMode mode = StoreMode.Set, string key = null, string value = null)
        {
            if (string.IsNullOrEmpty(key))
            {
                key = GetUniqueKey("store");
            }

            if (value == null)
            {
                value = GetRandomString();
            }
            return client.ExecuteStore(mode, key, value, ts);
        }

        public static string GetUniqueKey(string prefix = null)
        {
            return (!string.IsNullOrEmpty(prefix) ? prefix + "_" : "") +
                "unit_test_" + DateTime.Now.Ticks;
        }

        public static IEnumerable<string> GetUniqueKeys(string prefix = null, int max = 5)
        {
            var keys = new List<string>(max);
            for (int i = 0; i < max; i++)
            {
                keys.Add(GetUniqueKey(prefix));
            }

            return keys;
        }

        public static string GetRandomString()
        {
            var rand = new Random((int)DateTime.Now.Ticks).Next();
            return "unit_test_value_" + rand;
        }

        public static void StoreAssertPass(IStoreOperationResult result)
        {
            Assert.That(result.Success, Is.True, "Success was false");
            Assert.That(result.Cas, Is.GreaterThan(0), "Cas value was 0");
            Assert.That(result.StatusCode, Is.EqualTo(0), "StatusCode was not 0");
        }

        public static void StoreAssertFail(IStoreOperationResult result)
        {
            Assert.That(result.Success, Is.False, "Success was true");
            Assert.That(result.Cas, Is.EqualTo(0), "Cas value was not 0");
            Assert.That(result.StatusCode, Is.GreaterThan(0), "StatusCode not greater than 0");
            Assert.That(result.InnerResult, Is.Not.Null, "InnerResult was null");
        }

        public static void GetAssertPass(IGetOperationResult result, object expectedValue)
        {
            Assert.That(result.Success, Is.True, "Success was false");
            Assert.That(result.Cas, Is.GreaterThan(0), "Cas value was 0");
            Assert.That(result.StatusCode, Is.EqualTo(0).Or.Null, "StatusCode was neither 0 nor null");
            Assert.That(result.Value, Is.EqualTo(expectedValue), "Actual value was not expected value: " + result.Value);
        }

        public static void GetAssertFail(IGetOperationResult result)
        {
            Assert.That(result.Success, Is.False, "Success was true");
            Assert.That(result.Cas, Is.EqualTo(0), "Cas value was not 0");
            Assert.That(result.StatusCode, Is.Null.Or.GreaterThan(0), "StatusCode not greater than 0");
            Assert.That(result.HasValue, Is.False, "HasValue was true");
            Assert.That(result.Value, Is.Null, "Value was not null");
        }

        public static void MutateAssertPass(IMutateOperationResult result, ulong expectedValue)
        {
            Assert.That(result.Success, Is.True, "Success was false");
            Assert.That(result.Value, Is.EqualTo(expectedValue), "Value was not expected value: " + expectedValue);
            Assert.That(result.Cas, Is.GreaterThan(0), "Cas was not greater than 0");
            Assert.That(result.StatusCode, Is.Null.Or.EqualTo(0), "StatusCode was not null or 0");
        }

        public static void MutateAssertFail(IMutateOperationResult result)
        {
            Assert.That(result.Success, Is.False, "Success was true");
            Assert.That(result.Cas, Is.EqualTo(0), "Cas 0");
            Assert.That(result.StatusCode, Is.Null.Or.Not.EqualTo(0), "StatusCode was 0");
        }

        public static void ConcatAssertPass(IConcatOperationResult result)
        {
            Assert.That(result.Success, Is.True, "Success was false");
            Assert.That(result.Cas, Is.GreaterThan(0), "Cas value was 0");
            Assert.That(result.StatusCode, Is.EqualTo(0), "StatusCode was not 0");
        }

        public static void ConcatAssertFail(IConcatOperationResult result)
        {
            Assert.That(result.Success, Is.False, "Success was true");
            Assert.That(result.Cas, Is.EqualTo(0), "Cas value was not 0");
            Assert.That(result.StatusCode, Is.Null.Or.GreaterThan(0), "StatusCode not greater than 0");
        }
    }
}