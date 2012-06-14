using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Couchbase.Configuration;
using NUnit.Framework.Constraints;
using System.Net;
using Couchbase.Management;
using Enyim.Caching.Memcached;

namespace Couchbase.Tests
{
	[TestFixture]
	public class CouchbaseClusterBucketAdminTests : CouchbaseClusterTestsBase
	{
		[Test]
		public void When_Listing_Buckets_Default_Bucket_Is_Returned()
		{
			var buckets = _Cluster.ListBuckets();
			Assert.That(buckets.FirstOrDefault(b => b.Name == "default"), Is.Not.Null, "default bucket was not found");
		}

		[Test]
		[ExpectedException(typeof(ArgumentNullException))]
		public void When_Listing_Buckets_With_Invalid_Config_Argument_Exception_Is_Thrown()
		{
			var config = new CouchbaseClientConfiguration();
			config.Urls.Add(new Uri("http://localhost:8091/doesnotexist/"));
			config.Bucket = "default";
			var server = new CouchbaseCluster(config);
			var buckets = server.ListBuckets();
		}

		[Test]
		public void When_Try_Listing_Buckets_Default_Bucket_Is_Returned()
		{
			Bucket[] buckets;
			var result = _Cluster.TryListBuckets(out buckets);
			Assert.That(buckets.FirstOrDefault(b => b.Name == "default"), Is.Not.Null, "default bucket was not found");
			Assert.That(result, Is.True);
		}

		[Test]
		public void When_Try_Listing_Buckets_With_Invalid_Config_No_Exception_Is_Thrown()
		{
			var config = new CouchbaseClientConfiguration();
			config.Urls.Add(new Uri("http://localhost:8091/doesnotexist/"));
			config.Bucket = "default";

			var server = new CouchbaseCluster(config);
			Bucket[] buckets;
			var result = _Cluster.TryListBuckets(out buckets);
		}

		[Test]
		public void When_Flushing_Bucket_Data_Are_Removed()
		{
			var config = new CouchbaseClientConfiguration();
			config.Urls.Add(new Uri("http://localhost:8091/pools/default"));
			config.Bucket = "default";

			var client = new CouchbaseClient(config);
			var storeResult = client.ExecuteStore(StoreMode.Set, "SomeKey", "SomeValue");

			Assert.That(storeResult.Success, Is.True);

			var getResult = client.ExecuteGet<string>("SomeKey");
			Assert.That(getResult.Success, Is.True);
			Assert.That(getResult.Value, Is.StringMatching("SomeValue"));

			_Cluster.FlushBucket("default");

			getResult = client.ExecuteGet<string>("SomeKey");
			Assert.That(getResult.Success, Is.False);
			Assert.That(getResult.Value, Is.Null);
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