using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached.Results;
using Enyim.Caching.Memcached;
using Couchbase.Configuration;
using Couchbase.Tests.Factories;
using Couchbase.Management;
using System.IO;

namespace Couchbase.Tests
{

	[TestFixture]
	public abstract class CouchbaseClientTestsBase
	{
		protected ICouchbaseClient Client;
	    private static int _numberOfTimesCalled = 0;

		[TestFixtureSetUp]
		public void SetUp()
		{
            Client = CouchbaseClientFactory.CreateCouchbaseClient();

		    if (_numberOfTimesCalled < 1)
		    {
                var cluster = CouchbaseClusterFactory.CreateCouchbaseCluster();
		        using (var stream = File.Open(@"Data\\ThingViews.json", FileMode.Open))
		        {
		            cluster.CreateDesignDocument("default", "things", stream);
		        }
		        Interlocked.Increment(ref _numberOfTimesCalled);
		    }
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