using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
			_Client.FlushAll();
		}

		[Test]
		public void When_FlushAll_Is_Called_On_BinaryNode_No_Exception_Is_Raised()
		{
			var config = ConfigurationManager.GetSection("memcached-config") as CouchbaseClientSection;
			var client = new CouchbaseClient(config);
			client.FlushAll();
		}
	}
}
