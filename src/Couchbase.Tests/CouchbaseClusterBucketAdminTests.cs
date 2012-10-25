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
using System.Threading;
using System.IO;
using System.Text.RegularExpressions;
using Couchbase.Tests.Factories;
using Couchbase.Operations;

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
		public void When_Try_Listing_Buckets_With_Invalid_Config_No_Exception_Is_Thrown_And_Return_Value_Is_False()
		{
			var config = new CouchbaseClientConfiguration();
			config.Urls.Add(new Uri("http://localhost:8091/doesnotexist/"));
			config.Bucket = "default";

			var server = new CouchbaseCluster(config);
			Bucket[] buckets;
			var result = server.TryListBuckets(out buckets);
			Assert.That(result, Is.False);
		}

		[Test]
		public void When_Getting_Bucket_That_Bucket_Is_Not_Null()
		{
			var bucketName = "Bucket-" + DateTime.Now.Ticks;

			_Cluster.CreateBucket(new Bucket
			{
				Name = bucketName,
				AuthType = AuthTypes.Sasl,
				BucketType = BucketTypes.Membase,
				RamQuotaMB = 100
			}
			);

			var bucket = waitForBucket(bucketName);

			Assert.That(bucket, Is.Not.Null);

			_Cluster.DeleteBucket(bucketName);
		}

		[ExpectedException(typeof(WebException))]
		[Test]
		public void When_Getting_Invalid_Bucket_Web_Exception_Is_Thrown()
		{
			var bucketName = "Bucket-" + DateTime.Now.Ticks;

			_Cluster.CreateBucket(new Bucket
			{
				Name = bucketName,
				AuthType = AuthTypes.Sasl,
				BucketType = BucketTypes.Membase,
				RamQuotaMB = 100
			}
			);

			var bucket = waitForBucket(bucketName);

			Assert.That(bucket, Is.Not.Null);

			_Cluster.DeleteBucket(bucketName);

		}

		[Test]
		public void When_Try_Getting_Bucket_That_Bucket_Is_Not_Null()
		{
			var bucketName = "Bucket-" + DateTime.Now.Ticks;

			_Cluster.CreateBucket(new Bucket
			{
				Name = bucketName,
				AuthType = AuthTypes.Sasl,
				BucketType = BucketTypes.Membase,
				RamQuotaMB = 100
			}
			);

			var bucket = tryWaitForBucket(bucketName);

			Assert.That(bucket, Is.Not.Null);

			_Cluster.DeleteBucket(bucketName);
		}

		[Test]
		public void When_Try_Getting_Invalid_Bucket_Web_Exception_Is_Not_Thrown()
		{

			var bucket = tryWaitForBucket("ShouldNotExist");
			Assert.That(bucket, Is.Null);

		}

		[Ignore("Restful flush has been delayed")]
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

		[Test]
		public void When_Creating_New_Bucket_That_Bucket_Is_Listed()
		{
			var bucketName = "Bucket-" + DateTime.Now.Ticks;

			_Cluster.CreateBucket(new Bucket
			{
				Name = bucketName,
				AuthType = AuthTypes.Sasl,
				BucketType = BucketTypes.Membase,
				RamQuotaMB = 100
			}
			);

			var bucket = waitForListedBucket(bucketName);

			Assert.That(bucket, Is.Not.Null);

			_Cluster.DeleteBucket(bucketName);

		}

		[Test]
		public void When_Creating_New_Memcached_Bucket_That_Bucket_Is_Listed()
		{
			var bucketName = "Bucket-" + DateTime.Now.Ticks;

			_Cluster.CreateBucket(new Bucket
			{
				Name = bucketName,
				AuthType = AuthTypes.None,
				BucketType = BucketTypes.Memcached,
				RamQuotaMB = 100,
				ProxyPort = 9090
			}
			);

			var bucket = waitForListedBucket(bucketName);

			Assert.That(bucket, Is.Not.Null);

			_Cluster.DeleteBucket(bucketName);

		}

		[Test]
		[ExpectedException(typeof(WebException))]
		public void When_Creating_New_Bucket_With_Existing_Name_Web_Exception_Is_Thrown()
		{
			_Cluster.CreateBucket(new Bucket
			{
				Name = "default",
				AuthType = AuthTypes.Sasl,
				BucketType = BucketTypes.Membase,
				RamQuotaMB = 128
			});
		}

		[Test]
		[ExpectedException(typeof(ArgumentException), ExpectedMessage = "ProxyPort", MatchType = MessageMatch.Contains)]
		public void When_Creating_New_Bucket_With_Auth_Type_None_And_No_Port_Argument_Exception_Is_Thrown()
		{
			_Cluster.CreateBucket(new Bucket
			{
				Name = "default",
				AuthType = AuthTypes.None,
				BucketType = BucketTypes.Memcached,
				RamQuotaMB = 128
			});
		}

		[Test]
		[ExpectedException(typeof(ArgumentException), ExpectedMessage = "ProxyPort", MatchType = MessageMatch.Contains)]
		public void When_Creating_New_Bucket_With_Auth_Type_Sasl_And_Port_Argument_Exception_Is_Thrown()
		{
			_Cluster.CreateBucket(new Bucket
			{
				Name = "default",
				AuthType = AuthTypes.None,
				BucketType = BucketTypes.Memcached,
				RamQuotaMB = 128
			});
		}


		[Test]
		[ExpectedException(typeof(ArgumentException), ExpectedMessage = "RamQuotaMB", MatchType = MessageMatch.Contains)]
		public void When_Creating_New_Bucket_With_Ram_Quota_Less_Than_100_Argument_Exception_Is_Thrown()
		{
			_Cluster.CreateBucket(new Bucket
			{
				Name = "default",
				AuthType = AuthTypes.Sasl,
				BucketType = BucketTypes.Memcached,
				RamQuotaMB = 99
			});
		}

		[Test]
		public void When_Deleting_Bucket_Bucket_Is_No_Longer_Listed()
		{
			var bucketName = "Bucket-" + DateTime.Now.Ticks;

			_Cluster.CreateBucket(new Bucket
			{
				Name = bucketName,
				AuthType = AuthTypes.Sasl,
				BucketType = BucketTypes.Membase,
				RamQuotaMB = 100
			});

			var bucket = waitForListedBucket(bucketName);

			Assert.That(bucket, Is.Not.Null, "New bucket was null");

			_Cluster.DeleteBucket(bucketName);

			bucket = waitForListedBucket(bucketName);

			Assert.That(bucket, Is.Null, "Deleted bucket still exists");
		}

		[Test]
		[ExpectedException(typeof(WebException), ExpectedMessage = "404", MatchType = MessageMatch.Contains)]
		public void When_Deleting_Bucket_That_Does_Not_Exist_404_Web_Exception_Is_Thrown()
		{
			var bucketName = "Bucket-" + DateTime.Now.Ticks;

			_Cluster.DeleteBucket(bucketName);
		}

		[Test]
		public void When_Creating_New_Bucket_Item_Counts_Are_Set_On_Basic_Stats()
		{
			var bucketName = "Bucket-" + DateTime.Now.Ticks;

			_Cluster.CreateBucket(new Bucket
			{
				Name = bucketName,
				AuthType = AuthTypes.Sasl,
				BucketType = BucketTypes.Membase,
				RamQuotaMB = 100
			});

			var bucket = waitForListedBucket(bucketName);
			Assert.That(bucket, Is.Not.Null, "New bucket was null");

			var count = bucket.BasicStats.ItemCount;
			Assert.That(count, Is.EqualTo(0), "Item count was not 0");

			var client = new CouchbaseClient(bucketName, "");

			var result = false;
			for(var i = 0; i < 10; i++)
			{
				var aResult = client.ExecuteStore(StoreMode.Set, "a", "a");
				var bResult = client.ExecuteStore(StoreMode.Set, "b", "b");
				var cResult = client.ExecuteStore(StoreMode.Set, "c", "c");
				result = aResult.Success & bResult.Success & cResult.Success;
				if (result) break;
				Thread.Sleep(2000); //wait for the bucket to be ready for writing
			}
			Assert.That(result, Is.True, "Store operations failed");

			for (var i = 0; i < 10; i++)
			{
				bucket = _Cluster.ListBuckets().Where(b => b.Name == bucketName).FirstOrDefault();
				count = bucket.BasicStats.ItemCount;
				if (count == 3) break;
				Thread.Sleep(2000); //wait for the bucket to compute writes into basic stats
			}
			Assert.That(count, Is.EqualTo(3), "Item count was not 3");

			_Cluster.DeleteBucket(bucketName);
			bucket = waitForListedBucket(bucketName);
			Assert.That(bucket, Is.Null, "Deleted bucket still exists");
		}

		[Test]
		public void When_Listing_Bucket_Object_Graph_Is_Populated()
		{
			var bucketName = "Bucket-" + DateTime.Now.Ticks;

			_Cluster.CreateBucket(new Bucket
			{
				Name = bucketName,
				AuthType = AuthTypes.Sasl,
				BucketType = BucketTypes.Membase,
				RamQuotaMB = 100
			});

			var bucket = waitForListedBucket(bucketName);
			Assert.That(bucket, Is.Not.Null, "New bucket was null");

			Assert.That(bucket.VBucketServerMap, Is.Not.Null);
			Assert.That(bucket.VBucketServerMap.VBucketMap, Is.Not.Null);

			Assert.That(bucket.Quota, Is.Not.Null);
			Assert.That(bucket.DDocs, Is.Not.Null);
			Assert.That(bucket.Controllers, Is.Not.Null);
			Assert.That(bucket.BasicStats, Is.Not.Null);

			var node = bucket.Nodes.FirstOrDefault();
			Assert.That(node, Is.Not.Null, "Node was null");

			Assert.That(node.MemoryTotal, Is.GreaterThan(0));
			Assert.That(node.MemoryFree, Is.GreaterThan(0));
			Assert.That(node.Replication, Is.GreaterThanOrEqualTo(0));
			Assert.That(node.OS, Is.Not.Null);
			Assert.That(node.Version, Is.Not.Null);

			_Cluster.DeleteBucket(bucketName);

			bucket = waitForListedBucket(bucketName);

			Assert.That(bucket, Is.Null, "Deleted bucket still exists");
		}

		#region Design Documents
		[Test]
		public void When_Creating_Design_Document_Operation_Is_Successful()
		{
			var json =
@"{
    ""views"": {
        ""by_name"": {
            ""map"": ""function (doc) { if (doc.type == \""city\"") { emit(doc.name, null); } }""
        }
    }
}";
			var result = _Cluster.CreateDesignDocument("default", "cities", json);
			Assert.That(result, Is.True);
		}

		[Test]
		public void When_Creating_Design_Document_With_Stream_Operation_Is_Successful()
		{
			var stream = new FileStream("Data\\CityViews.json", FileMode.Open);
			Assert.That(stream.CanRead, Is.True);
			var result = _Cluster.CreateDesignDocument("default", "cities", stream);
			Assert.That(result, Is.True);
		}

		[Test]
		[ExpectedException(typeof(ArgumentException))]
		public void When_Creating_Design_Document_With_Invalid_Json_Argument_Exception_Is_Thrown()
		{
			_Cluster.CreateDesignDocument("default", "cities", "foo");
		}

		[Test]
		[ExpectedException(typeof(ArgumentException), ExpectedMessage = "nam", MatchType = MessageMatch.Contains)]
		public void When_Creating_Design_Document_With_Missing_Name_Argument_Exception_Is_Thrown()
		{
			var json = "{ \"id\" : \"foo\" }";
			_Cluster.CreateDesignDocument("default", "", json);
		}

		[Test]
		[ExpectedException(typeof(ArgumentException), ExpectedMessage = "nam", MatchType = MessageMatch.Contains)]
		public void When_Creating_Design_Document_With_Missing_Views_Argument_Exception_Is_Thrown()
		{
			var json = "{ \"notviews\" : \"foo\" }";
			_Cluster.CreateDesignDocument("default", "", json);
		}

		[Test]
		public void When_Retrieving_Design_Document_Operation_Is_Successful()
		{
			var json =
@"{
    ""views"": {
        ""by_name"": {
            ""map"": ""function (doc) { if (doc.type == \""city\"") { emit(doc.name, null); } }""
        }
    }
}";
			var result = _Cluster.CreateDesignDocument("default", "cities", json);
			Assert.That(result, Is.True);

			var clusterJson = _Cluster.RetrieveDesignDocument("default", "cities");
			Assert.That(Regex.Replace(json, @"\s", ""), Is.StringContaining(Regex.Replace(clusterJson, @"\s", "")));
		}

		[Test]
		[ExpectedException(typeof(WebException), ExpectedMessage="404", MatchType=MessageMatch.Contains)]
		public void When_Retrieving_Invalid_Design_Document_Operation_Web_Exception_Is_Thrown()
		{
			var result = _Cluster.RetrieveDesignDocument("foo", "bar");
		}

		[Test]
		public void When_Deleting_Design_Document_Operation_Is_Successful()
		{
			var json =
@"{
    ""views"": {
        ""by_name"": {
            ""map"": ""function (doc) { if (doc.type == \""city\"") { emit(doc.name, null); } }""
        }
    }
}";
			var result = _Cluster.CreateDesignDocument("default", "cities", json);
			Assert.That(result, Is.True);

			var clusterJson = _Cluster.RetrieveDesignDocument("default", "cities");
			Assert.That(Regex.Replace(json, @"\s", ""), Is.StringContaining(Regex.Replace(clusterJson, @"\s", "")));

			var deleteResult = _Cluster.DeleteDesignDocument("default", "cities");
			Assert.That(deleteResult, Is.True);

		}

		[Test]
		[ExpectedException(typeof(WebException), ExpectedMessage = "404", MatchType = MessageMatch.Contains)]
		public void When_Deleting_Invalid_Design_Document_Operation_Web_Exception_Is_Thrown()
		{
			var result = _Cluster.DeleteDesignDocument("foo", "bar");
		}

		[Test]
		public void When_Managing_Design_Document_On_Non_Default_Bucket_Operation_Is_Successful()
		{
			var bucketName = "Bucket-" + DateTime.Now.Ticks;
			var bucket = new Bucket
			{
				AuthType = AuthTypes.Sasl,
				BucketType = BucketTypes.Membase,
				Name = bucketName,
				Password = "qwerty",
				RamQuotaMB = 100
			};

			_Cluster.CreateBucket(bucket);
			var createdBucket = waitForListedBucket(bucket.Name);
			Assert.That(createdBucket, Is.Not.Null);

			var createResult = _Cluster.CreateDesignDocument(bucket.Name, "cities", new FileStream("Data\\CityViews.json", FileMode.Open));
			Assert.That(createResult, Is.True);

			var retrieveResult = _Cluster.RetrieveDesignDocument(bucket.Name, "cities");
			Assert.That(retrieveResult, Is.Not.Null);

			var deleteResult = _Cluster.DeleteDesignDocument(bucket.Name, "cities");
			Assert.That(deleteResult, Is.True);

			_Cluster.DeleteBucket(bucket.Name);
			var deletedBucket = waitForListedBucket(bucket.Name);
			Assert.That(deletedBucket, Is.Null);
		}

		#endregion

		private Bucket waitForListedBucket(string bucketName, int ubound = 10, int milliseconds = 1000)
		{
			Func<string, Bucket> func = (s) => _Cluster.ListBuckets().Where(b => b.Name == s).FirstOrDefault();
			return wait(bucketName, ubound, milliseconds, func);
		}

		private Bucket waitForBucket(string bucketName, int ubound = 10, int milliseconds = 1000)
		{
			return wait(bucketName, ubound, milliseconds, _Cluster.GetBucket);
		}

		private Bucket tryWaitForBucket(string bucketName, int ubound = 10, int milliseconds = 1000)
		{
			Func<string, Bucket> func = (s) =>
			{
				Bucket bucket = null;
				_Cluster.TryGetBucket(s, out bucket);
				return bucket;
			};
			return wait(bucketName, ubound, milliseconds, func);
		}

		private Bucket wait(string bucketName, int ubound, int milliseconds, Func<string, Bucket> getAction)
		{
			//Wait 10 seconds for bucket creation
			Bucket bucket = null;
			for (var i = 0; i < ubound; i++)
			{
				bucket = getAction(bucketName);
				if (bucket != null) break;
				Thread.Sleep(1000);
			}

			return bucket;
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