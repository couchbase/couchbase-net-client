using System;
using System.Collections.Generic;
using System.Configuration;
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

using System.Configuration;

namespace Couchbase.Tests
{
    [TestFixture]
    public class CouchbaseClusterBucketAdminTests : CouchbaseClusterTestsBase
    {
        private Bucket _bucket;

        /// <summary>
        /// @test: List buckets in cluster should return default bucket
        /// @pre: Default configuration to initialize client in app.config to initialize client in app.config and cluster should have a default bucket
        /// @post: Test passes if default bucket is found
        /// </summary>
        [Test]
        public void When_Listing_Buckets_Default_Bucket_Is_Returned()
        {
            var buckets = Cluster.ListBuckets();
            Assert.That(buckets.FirstOrDefault(b => b.Name == "default"), Is.Not.Null, "default bucket was not found");
        }

        /// <summary>
        /// @test: List buckets in cluster with invalid server configuration
        /// should return argument null exception
        /// @pre: Use incorrect configuration of server
        /// @post: Test passes if error is thrown
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void When_Listing_Buckets_With_Invalid_Config_Argument_Exception_Is_Thrown()
        {
            var config = new CouchbaseClientConfiguration();
            config.Urls.Add(new Uri(ConfigurationManager.AppSettings["CouchbaseServerUrl"] + "/doesnotexist/"));
            config.Bucket = "default";
            var server = new CouchbaseCluster(config);
            var buckets = server.ListBuckets();
        }

        /// <summary>
        /// @test: Try List buckets in cluster should return default bucket
        /// @pre: Default configuration to initialize client in app.config and cluster should have a default bucket
        /// @post: Test passes if default bucket is found
        /// </summary>
        [Test]
        public void When_Try_Listing_Buckets_Default_Bucket_Is_Returned()
        {
            Bucket[] buckets;
            var result = Cluster.TryListBuckets(out buckets);
            Assert.That(buckets.FirstOrDefault(b => b.Name == "default"), Is.Not.Null, "default bucket was not found");
            Assert.That(result, Is.True);
        }

        /// <summary>
        /// @test: Try List buckets in  with invalid configuration should return false
        /// and no exception is thrown
        /// @pre: Default configuration to initialize client in app.config an use invalid configuration
        /// @post: Test passes if default bucket is found
        /// </summary>
        [Test]
        public void When_Try_Listing_Buckets_With_Invalid_Config_No_Exception_Is_Thrown_And_Return_Value_Is_False()
        {
            var config = new CouchbaseClientConfiguration();
            config.Urls.Add(new Uri(ConfigurationManager.AppSettings["CouchbaseServerUrl"] + "/doesnotexist/"));
            config.Bucket = "default";

            var server = new CouchbaseCluster(config);
            Bucket[] buckets;
            var result = server.TryListBuckets(out buckets);
            Assert.That(result, Is.False);
        }

        /// <summary>
        /// @test: Create a SASL bucket and then retrieve its object and then delete it
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if bucket is created, retrieved and later removed successfully
        /// </summary>
        [Test]
        public void When_Getting_Bucket_That_Bucket_Is_Not_Null()
        {
            var bucketName = "Bucket-" + DateTime.Now.Ticks;

            Cluster.CreateBucket(new Bucket
            {
                Name = bucketName,
                AuthType = AuthTypes.Sasl,
                BucketType = BucketTypes.Membase,
                Quota = new Quota { RAM = 100 },
                ReplicaNumber = ReplicaNumbers.Zero
            });

            _bucket = waitForBucket(bucketName);
            Assert.That(_bucket, Is.Not.Null);
        }

        /// <summary>
        /// @test: create invalid bucket, while retrieving bucket, web exception is thrown
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if invalid bucket throws web exception
        /// </summary>
        [ExpectedException(typeof(WebException))]
        [Test]
        public void When_Getting_Invalid_Bucket_Web_Exception_Is_Thrown()
        {
            var bucketName = "Bucket-" + DateTime.Now.Ticks;

            _bucket = waitForBucket(bucketName);

            Assert.That(_bucket, Is.Not.Null);
        }

        /// <summary>
        /// @test: create sasl bucket and try get bucket should not be null
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if bucket is created and found
        /// </summary>
        [Test]
        public void When_Try_Getting_Bucket_That_Bucket_Is_Not_Null()
        {
            var bucketName = "Bucket-" + DateTime.Now.Ticks;

            Cluster.CreateBucket(new Bucket
            {
                Name = bucketName,
                AuthType = AuthTypes.Sasl,
                BucketType = BucketTypes.Membase,
                Quota = new Quota { RAM = 100 },
                ReplicaNumber = ReplicaNumbers.Zero
            }
            );

            _bucket = tryWaitForBucket(bucketName);
            Assert.That(_bucket, Is.Not.Null);
        }

        /// <summary>
        /// @test: Create bucket, getting bucket item count should match basic stats
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if bucket is created and the item count matches basic stats
        /// </summary>
        [Test]
        public void When_Getting_Bucket_Item_Count_Count_Matches_Basic_Stats()
        {
            var bucketName = "Bucket-" + DateTime.Now.Ticks;

            Cluster.CreateBucket(new Bucket
            {
                Name = bucketName,
                AuthType = AuthTypes.Sasl,
                BucketType = BucketTypes.Membase,
                Quota = new Quota { RAM = 100 },
                ReplicaNumber = ReplicaNumbers.Zero
            }
            );

            _bucket = waitForBucket(bucketName);
            Assert.That(_bucket, Is.Not.Null);
            long count = Cluster.GetItemCount(bucketName);
            Assert.That(count, Is.EqualTo(_bucket.BasicStats.ItemCount));
        }

        /// <summary>
        /// @test: Create bucket, getting cluster item count should match interesting stats
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if bucket is created and the item count matches interesting stats
        /// </summary>
        [Test]
        public void When_Getting_Cluster_Item_Count_Count_Matches_Interesting_Stats()
        {
            var bucketName = "Bucket-" + DateTime.Now.Ticks;

            Cluster.CreateBucket(new Bucket
            {
                Name = bucketName,
                AuthType = AuthTypes.Sasl,
                BucketType = BucketTypes.Membase,
                Quota = new Quota { RAM = 100 },
                ReplicaNumber = ReplicaNumbers.Zero
            }
            );

            _bucket = waitForBucket(bucketName);
            Assert.That(_bucket, Is.Not.Null);
            long count = Cluster.GetItemCount();
            Assert.That(count, Is.EqualTo(_bucket.Nodes.FirstOrDefault().InterestingStats.Curr_Items_Tot));
        }

        /// <summary>
        /// @test: Try get invalid bucket should throw an invalid exception
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if exception is thrown
        /// </summary>
        [Test]
        public void When_Try_Getting_Invalid_Bucket_Web_Exception_Is_Not_Thrown()
        {
            var bucket = tryWaitForBucket("ShouldNotExist");
            Assert.That(bucket, Is.Null);
        }

        [Test]
        public void When_Flushing_Bucket_Data_Are_Removed()
        {
            var storedConfig = ConfigurationManager.GetSection("couchbase") as ICouchbaseClientConfiguration;
            var config = new CouchbaseClientConfiguration();

            config.Bucket = "Bucket-" + DateTime.Now.Ticks;
            config.Username = storedConfig.Username;
            config.Password = storedConfig.Password;
            config.Urls.Add(storedConfig.Urls[0]);

            var cluster = new CouchbaseCluster(config);
            cluster.CreateBucket(new Bucket
                {
                    Name = config.Bucket,
                    AuthType = AuthTypes.Sasl,
                    BucketType = BucketTypes.Membase,
                    Quota = new Quota { RAM = 100 },
                    ReplicaNumber = ReplicaNumbers.Zero,
                    FlushOption = FlushOptions.Enabled
                }
            );

            for (int i = 0; i < 10; i++) //wait for bucket to be ready to accept ops
            {
                _bucket = waitForBucket(config.Bucket);
                if (_bucket.Nodes.First().Status == "healthy") break;
                Thread.Sleep(1000);
            }

            Assert.That(_bucket, Is.Not.Null);

            using (var client = new CouchbaseClient(config))
            {
                var storeResult = client.ExecuteStore(StoreMode.Set, "SomeKey", "SomeValue");
                Assert.That(storeResult.Success, Is.True, "Message: " + storeResult.Message);

                var getResult = client.ExecuteGet<string>("SomeKey");
                Assert.That(getResult.Success, Is.True);
                Assert.That(getResult.Value, Is.StringMatching("SomeValue"));

                cluster.FlushBucket(config.Bucket);

                getResult = client.ExecuteGet<string>("SomeKey");
                Assert.That(getResult.Success, Is.False);
                Assert.That(getResult.Value, Is.Null);
            }
        }

        /// <summary>
        /// @test: create bucket and verify that the bucket gets listed
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if bucket is listed correctly
        /// </summary>
        [Test]
        public void When_Creating_New_Bucket_That_Bucket_Is_Listed()
        {
            var bucketName = "Bucket-" + DateTime.Now.Ticks;

            Cluster.CreateBucket(new Bucket
            {
                Name = bucketName,
                AuthType = AuthTypes.Sasl,
                BucketType = BucketTypes.Membase,
                Quota = new Quota { RAM = 100 },
                ReplicaNumber = ReplicaNumbers.Zero
            });

            _bucket = waitForListedBucket(bucketName);
            Assert.That(_bucket, Is.Not.Null);
        }

        /// <summary>
        /// @test: create bucket and update it and verify that the bucket gets listed
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if updated bucket is listed correctly
        /// </summary>
        [Test]
        public void When_Updating_Bucket_That_Bucket_Is_Listed()
        {
            var bucketName = "Bucket-" + DateTime.Now.Ticks;

            Cluster.CreateBucket(new Bucket
            {
                Name = bucketName,
                AuthType = AuthTypes.Sasl,
                BucketType = BucketTypes.Membase,
                Quota = new Quota { RAM = 100 },
                ReplicaNumber = ReplicaNumbers.Zero
            });

            _bucket = waitForListedBucket(bucketName);
            Assert.That(_bucket, Is.Not.Null);

            Cluster.UpdateBucket(new Bucket
            {
                Name = bucketName,
                Quota = new Quota { RAM = 105 },
                AuthType = AuthTypes.None,
                ProxyPort = 8675
            });

            _bucket = waitForListedBucket(bucketName);

            Assert.That(_bucket.Quota.RAM / 1024 / 1024, Is.EqualTo(105));
            Assert.That(_bucket.ProxyPort, Is.EqualTo(8675));
            Assert.That(_bucket.AuthType, Is.EqualTo(AuthTypes.None));
        }

        /// <summary>
        /// @test: create new memcached bucket and verify that the bucket gets listed
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if bucket is listed correctly
        /// </summary>
        [Test]
        public void When_Creating_New_Memcached_Bucket_That_Bucket_Is_Listed()
        {
            var bucketName = "Bucket-" + DateTime.Now.Ticks;

            Cluster.CreateBucket(new Bucket
            {
                Name = bucketName,
                AuthType = AuthTypes.None,
                BucketType = BucketTypes.Memcached,
                Quota = new Quota {RAM = 100},
                ProxyPort = 9090,
                ReplicaNumber = ReplicaNumbers.Zero
            });

            _bucket = waitForListedBucket(bucketName);
            Assert.That(_bucket, Is.Not.Null);
        }

        /// <summary>
        /// @test: create new bucket with existing name and verify that the exception is thrown
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if error is thrown
        /// </summary>
        [Test]
        [ExpectedException(typeof(WebException))]
        public void When_Creating_New_Bucket_With_Existing_Name_Web_Exception_Is_Thrown()
        {
            var bucket = new Bucket
            {
                Name = "default",
                AuthType = AuthTypes.Sasl,
                BucketType = BucketTypes.Membase,
                Quota = new Quota {RAM = 128},
                ReplicaNumber = ReplicaNumbers.Zero
            };
            Cluster.CreateBucket(bucket);
        }

        /// <summary>
        /// @test: create bucket with no authorizaton and port and verify that the error is thrown
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if error is thrown
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException), ExpectedMessage = "ProxyPort", MatchType = MessageMatch.Contains)]
        public void When_Creating_New_Bucket_With_Auth_Type_None_And_No_Port_Argument_Exception_Is_Thrown()
        {
            var bucket = new Bucket
            {
                Name = "default",
                AuthType = AuthTypes.None,
                BucketType = BucketTypes.Memcached,
                Quota = new Quota {RAM = 128},
                ReplicaNumber = ReplicaNumbers.Zero
            };
            Cluster.CreateBucket(bucket);
        }

        /// <summary>
        /// @test: create sasl bucket with no authorization type and verify that the error is thrown
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if error is thrown
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException), ExpectedMessage = "ProxyPort", MatchType = MessageMatch.Contains)]
        public void When_Creating_New_Bucket_With_Auth_Type_Sasl_And_Port_Argument_Exception_Is_Thrown()
        {
            var bucket = new Bucket
            {
                Name = "default",
                AuthType = AuthTypes.None,
                BucketType = BucketTypes.Memcached,
                Quota = new Quota {RAM = 128},
                ReplicaNumber = ReplicaNumbers.Zero
            };
            Cluster.CreateBucket(bucket);
        }

        /// <summary>
        /// @test: create bucket with RAM quota less than 100 and verify that the error is thrown
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if bucket is error is thrown
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException), ExpectedMessage = "RamQuotaMB must be at least 100", MatchType = MessageMatch.Contains)]
        public void When_Creating_New_Bucket_With_Ram_Quota_Less_Than_100_Argument_Exception_Is_Thrown()
        {
            var bucket = new Bucket
            {
                Name = "default",
                AuthType = AuthTypes.Sasl,
                BucketType = BucketTypes.Memcached,
                Quota = new Quota {RAM = 99},
                ReplicaNumber = ReplicaNumbers.Zero
            };
            Cluster.CreateBucket(bucket);
        }

        /// <summary>
        /// @test: create and delete bucket and verify that the bucket no longer is listed
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if bucket is not listed after deletion
        /// </summary>
        [Test]
        public void When_Deleting_Bucket_Bucket_Is_No_Longer_Listed()
        {
            var bucketName = "Bucket-" + DateTime.Now.Ticks;
            _bucket = new Bucket
            {
                Name = bucketName,
                AuthType = AuthTypes.Sasl,
                BucketType = BucketTypes.Membase,
                Quota = new Quota {RAM = 100},
                ReplicaNumber = ReplicaNumbers.Zero
            };
            Cluster.CreateBucket(_bucket);

            _bucket = waitForListedBucket(bucketName);
            Assert.That(_bucket, Is.Not.Null, "New bucket was null");

            _bucket = waitForListedBucket(bucketName);
            Assert.That(_bucket, Is.Null, "Deleted bucket still exists");
        }

        /// <summary>
        /// @test: delete bucket that does not exist and exception is thrown
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if deleting bucket throws an error
        /// </summary>
        [Test]
        [Ignore]
        //TODO: When a bucket doesn't exist, the request throws a 500, but a 500 also might be thrown if the
        //delete takes longer than the server expects it to.  Though in this case, the delete is sucessful
        //[ExpectedException(typeof(WebException), ExpectedMessage = "404", MatchType = MessageMatch.Contains)]
        public void When_Deleting_Bucket_That_Does_Not_Exist_404_Web_Exception_Is_Thrown()
        {
            var bucketName = "Bucket-" + DateTime.Now.Ticks;

            Cluster.DeleteBucket(bucketName);
        }

        /// <summary>
        /// @test: create new bucket, wait that it gets listed, and verify that bucket counts are set on basic stats
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if deleting bucket throws an error
        /// </summary>
        [Test]
        public void When_Creating_New_Bucket_Item_Counts_Are_Set_On_Basic_Stats()
        {
            var bucketName = "Bucket-" + DateTime.Now.Ticks;
            _bucket = new Bucket
            {
                Name = bucketName,
                AuthType = AuthTypes.Sasl,
                BucketType = BucketTypes.Membase,
                Quota = new Quota {RAM = 100},
                ReplicaNumber = ReplicaNumbers.Zero
            };
            Cluster.CreateBucket(_bucket);

            _bucket = waitForListedBucket(bucketName);
            Assert.That(_bucket, Is.Not.Null, "New bucket was null");

            var count = _bucket.BasicStats.ItemCount;
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
                _bucket = Cluster.ListBuckets().Where(b => b.Name == bucketName).FirstOrDefault();
                count = _bucket.BasicStats.ItemCount;
                if (count == 3) break;
                Thread.Sleep(2000); //wait for the bucket to compute writes into basic stats
            }
            Assert.That(count, Is.EqualTo(3), "Item count was not 3");

            Cluster.DeleteBucket(bucketName);
            _bucket = waitForListedBucket(bucketName);
            Assert.That(_bucket, Is.Null, "Deleted bucket still exists");
        }

        /// <summary>
        /// @test: create bucket and then list the bucket, object graph should be populated
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if graph is populated
        /// </summary>
        public void When_Listing_Bucket_Object_Graph_Is_Populated()
        {
            var bucketName = "Bucket-" + DateTime.Now.Ticks;
            _bucket = new Bucket
            {
                Name = bucketName,
                AuthType = AuthTypes.Sasl,
                BucketType = BucketTypes.Membase,
                Quota = new Quota {RAM = 100},
            };

            Cluster.CreateBucket(_bucket);

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

            Cluster.DeleteBucket(bucketName);

            _bucket = waitForListedBucket(bucketName);

            Assert.That(bucket, Is.Null, "Deleted bucket still exists");
        }

        #region Design Documents

        /// <summary>
        /// @test: create design document and verify that the operation is successful
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if operation is successful
        /// </summary>
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
            var result = Cluster.CreateDesignDocument("default", "cities", json);
            Assert.That(result, Is.True);
        }

        /// <summary>
        /// @test: create stream to read json file and use stream to create design document
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if operation to create design document is successful
        /// </summary>
        [Test]
        public void When_Creating_Design_Document_With_Stream_Operation_Is_Successful()
        {
            var stream = new FileStream("Data\\CityViews.json", FileMode.Open);
            Assert.That(stream.CanRead, Is.True);
            var result = Cluster.CreateDesignDocument("default", "cities", stream);
            Assert.That(result, Is.True);
        }

        /// <summary>
        /// @test: create design document with invalid json argument and exception should occur
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if exception occurs
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void When_Creating_Design_Document_With_Invalid_Json_Argument_Exception_Is_Thrown()
        {
            Cluster.CreateDesignDocument("default", "cities", "foo");
        }

        /// <summary>
        /// @test: create design document with missing argument and exception should occur
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if exception occurs
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException), ExpectedMessage = "nam", MatchType = MessageMatch.Contains)]
        public void When_Creating_Design_Document_With_Missing_Name_Argument_Exception_Is_Thrown()
        {
            var json = "{ \"id\" : \"foo\" }";
            Cluster.CreateDesignDocument("default", "", json);
        }

        /// <summary>
        /// @test: create design document with missing view argument and exception should occur
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if exception occurs
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException), ExpectedMessage = "nam", MatchType = MessageMatch.Contains)]
        public void When_Creating_Design_Document_With_Missing_Views_Argument_Exception_Is_Thrown()
        {
            var json = "{ \"notviews\" : \"foo\" }";
            Cluster.CreateDesignDocument("default", "", json);
        }

        /// <summary>
        /// @test: create design document and then retrieve it, operation should succeed
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if operaton is successful
        /// </summary>
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
            var result = Cluster.CreateDesignDocument("default", "cities", json);
            Assert.That(result, Is.True);

            var clusterJson = Cluster.RetrieveDesignDocument("default", "cities");
            Assert.That(Regex.Replace(json, @"\s", ""), Is.StringContaining(Regex.Replace(clusterJson, @"\s", "")));
        }

        /// <summary>
        /// @test: retrieve design document that is invalid, exception should occur
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if exception occurs
        /// </summary>
        [Test]
        [ExpectedException(typeof(WebException), ExpectedMessage="404", MatchType=MessageMatch.Contains)]
        public void When_Retrieving_Invalid_Design_Document_Operation_Web_Exception_Is_Thrown()
        {
            var result = Cluster.RetrieveDesignDocument("foo", "bar");
        }

        /// <summary>
        /// @test: create design document and then delete it, operation should be successful
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if deletion happens successfully
        /// </summary>
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
            var result = Cluster.CreateDesignDocument("default", "cities", json);
            Assert.That(result, Is.True);

            var clusterJson = Cluster.RetrieveDesignDocument("default", "cities");
            Assert.That(Regex.Replace(json, @"\s", ""), Is.StringContaining(Regex.Replace(clusterJson, @"\s", "")));

            var deleteResult = Cluster.DeleteDesignDocument("default", "cities");
            Assert.That(deleteResult, Is.True);
        }

        /// <summary>
        /// @test: delete invalid design document and exception should occur
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if exception occurs
        /// </summary>
        [Test]
        [ExpectedException(typeof(WebException), ExpectedMessage = "404", MatchType = MessageMatch.Contains)]
        public void When_Deleting_Invalid_Design_Document_Operation_Web_Exception_Is_Thrown()
        {
            var result = Cluster.DeleteDesignDocument("foo", "bar");
        }

        /// <summary>
        /// @test: create design document, retrieve and delete it using a non default bucket and
        /// verify that the operation is successful
        /// @pre: non default configuration to initialize client in app.config
        /// @post: Test passes if all the operations happen successfully
        /// </summary>
        [Test]
        public void When_Managing_Design_Document_On_Non_Default_Bucket_Operation_Is_Successful()
        {
            var bucketName = "Bucket-" + DateTime.Now.Ticks;
            _bucket = new Bucket
            {
                AuthType = AuthTypes.Sasl,
                BucketType = BucketTypes.Membase,
                Name = bucketName,
                Password = "qwerty",
                Quota = new Quota { RAM = 100 },
            };

            Cluster.CreateBucket(_bucket);
            var createdBucket = waitForListedBucket(_bucket.Name);
            Assert.That(createdBucket, Is.Not.Null);

            var createResult = Cluster.CreateDesignDocument(_bucket.Name, "cities", new FileStream("Data\\CityViews.json", FileMode.Open));
            Assert.That(createResult, Is.True);

            var retrieveResult = Cluster.RetrieveDesignDocument(_bucket.Name, "cities");
            Assert.That(retrieveResult, Is.Not.Null);

            var deleteResult = Cluster.DeleteDesignDocument(_bucket.Name, "cities");
            Assert.That(deleteResult, Is.True);

            Cluster.DeleteBucket(_bucket.Name);
            var deletedBucket = waitForListedBucket(_bucket.Name);
            Assert.That(deletedBucket, Is.Null);
        }

        #endregion

        private Bucket waitForListedBucket(string bucketName, int ubound = 10, int milliseconds = 1000)
        {
            Func<string, Bucket> func = (s) => Cluster.ListBuckets().Where(b => b.Name == s).FirstOrDefault();
            return wait(bucketName, ubound, milliseconds, func);
        }

        private Bucket waitForBucket(string bucketName, int ubound = 10, int milliseconds = 1000)
        {
            return wait(bucketName, ubound, milliseconds, Cluster.GetBucket);
        }

        private Bucket tryWaitForBucket(string bucketName, int ubound = 10, int milliseconds = 1000)
        {
            Func<string, Bucket> func = (s) =>
            {
                Bucket bucket = null;
                Cluster.TryGetBucket(s, out bucket);
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

        [TearDown]
        public void TearDown()
        {
            if (_bucket != null && _bucket.Name != "default" && Cluster != null)
            {
                Cluster.DeleteBucket(_bucket.Name);
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