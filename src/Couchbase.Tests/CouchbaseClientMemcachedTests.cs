using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Couchbase.Tests.Factories;
using Enyim.Caching.Memcached;
using NUnit.Framework;
using Couchbase.Configuration;
using System.Configuration;
using Enyim.Caching;

namespace Couchbase.Tests
{
	[TestFixture]
	public class CouchbaseClientMemcachedTests : CouchbaseClientTestsBase
	{
		[ExpectedException(typeof(NotImplementedException))]
		[Test]
		public void When_FlushAll_Is_Called_On_CouchbaseNode_Not_Implemented_Exception_Is_Raised()
		{
			Client.FlushAll();
		}

		[Test]
		public void When_FlushAll_Is_Called_On_BinaryNode_No_Exception_Is_Raised()
		{
			var config = ConfigurationManager.GetSection("memcached-config") as CouchbaseClientSection;
		    using (var client = new CouchbaseClient(config))
		    {
		        client.FlushAll();
		    }
		}

        const string Key = "1235key";
        [Test]
        public void Test_Store_StoreMode_Set()
        {
            var client = new CouchbaseClient("memcached-config");
            var result = client.Store(StoreMode.Set, Key, "value");
            Assert.AreEqual(result, true);
        }

        [Test]
        public void Test_Store_StoreMode_Add_Will_Fail_If_Key_Exists()
        {
            var client = new CouchbaseClient("memcached-config");
            var result = client.Store(StoreMode.Add, Key, "value");
            Assert.AreEqual(result, false);
        }
    }
}
