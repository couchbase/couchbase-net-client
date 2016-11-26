using System;
using System.Collections.Generic;
using System.Configuration;
using System.Security.Authentication;
using System.Threading;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.IntegrationTests.Utils;
using NUnit.Framework;

namespace Couchbase.IntegrationTests
{
    [TestFixture]
    public class ClusterHelperTests
    {
        private ICluster _cluster;

        [Test]
        public void When_Get_Called_Without_Calling_Initialize_InitializationException_Is_Thrown()
        {
            ClusterHelper.Close();
            var ex = Assert.Throws<InitializationException>(() => ClusterHelper.Get());
        }

        [Test]
        public void Test_OpenBucket()
        {
            ClusterHelper.Initialize(TestConfiguration.GetCurrentConfiguration());
            var cluster = ClusterHelper.Get();
            var bucket = cluster.OpenBucket();
            Assert.AreEqual("default", bucket.Name);
        }

        [Test]
        public void Test_GetBucket_Using_HttpStreamingProvider()
        {
            ClusterHelper.Initialize(TestConfiguration.GetCurrentConfiguration());
            _cluster = ClusterHelper.Get();

            const string expected = "default";
            using (var bucket = _cluster.OpenBucket("default"))
            {
                Assert.AreEqual(expected, bucket.Name);
            }
        }

        [Test]
        public void Test_GetBucket_Using_CarrierPublicationProvider()
        {
            ClusterHelper.Initialize(TestConfiguration.GetCurrentConfiguration());
            _cluster = ClusterHelper.Get();

            const string expected = "default";
            using (var bucket = _cluster.OpenBucket("default"))
            {
                Assert.IsNotNull(bucket);
                Assert.AreEqual(expected, bucket.Name);
            }
        }

        [Test]
        public void When_Initialized_Get_Returns_Instance()
        {
            ClusterHelper.Initialize(TestConfiguration.GetCurrentConfiguration());
            var cluster = ClusterHelper.Get();
            Assert.IsNotNull(cluster);
            cluster.Dispose();
        }

        [Test]
        public void When_OpenBucket_Is_Called_Multiple_Times_Same_Bucket_Object_IsReturned()
        {
            ClusterHelper.Initialize(TestConfiguration.GetCurrentConfiguration());
            _cluster = ClusterHelper.Get();

            var bucket1 = _cluster.OpenBucket("default");
            var bucket2 = _cluster.OpenBucket("default");

            Assert.AreSame(bucket1, bucket2);
        }

        [Test]
        public void When_GetBucket_Is_Called_Multiple_Times_Same_Bucket_Object_IsReturned()
        {
            ClusterHelper.Initialize(TestConfiguration.GetCurrentConfiguration());

            var bucket1 = ClusterHelper.GetBucket("default");
            var bucket2 = ClusterHelper.GetBucket("default");

            Assert.AreEqual(bucket1, bucket2);
        }

        [Test]
        public void When_Close_Called_Bucket_Count_Is_Zero()
        {
            ClusterHelper.Initialize(TestConfiguration.GetCurrentConfiguration());

            Assert.AreEqual(0, ClusterHelper.Count());
            var bucket1 = ClusterHelper.GetBucket("default");
            var bucket2 = ClusterHelper.GetBucket("default");
            Assert.AreEqual(1, ClusterHelper.Count());
            ClusterHelper.Close();
            Assert.AreEqual(0, ClusterHelper.Count());
        }

        [Test]
        public void When_RemoveBucket_Is_Called_Bucket_Count_Is_Zero()
        {
            ClusterHelper.Initialize(TestConfiguration.GetCurrentConfiguration());

            //open a bucket and get the reference
            var bucket1 = ClusterHelper.GetBucket("default");
            var bucket2 = ClusterHelper.GetBucket("default");

            Assert.AreEqual(1, ClusterHelper.Count());
            ClusterHelper.RemoveBucket("default");
            Assert.AreEqual(0, ClusterHelper.Count());
        }

        static readonly CountdownEvent TwoThreadsCompleted = new CountdownEvent(2);
        [Test]
        public void When_Bucket_Is_Opened_On_Two_Seperate_Threads_And_RemoveBucket_Is_Called_Count_Is_Zero()
        {
            ClusterHelper.Initialize(TestConfiguration.GetCurrentConfiguration());
            var t1 = new Thread(OpenBucket);
            var t2 = new Thread(OpenBucket);

            t1.Start();
            t2.Start();

            TwoThreadsCompleted.Wait();
            Assert.AreEqual(1, ClusterHelper.Count());
            ClusterHelper.RemoveBucket("default");
            Assert.AreEqual(0, ClusterHelper.Count());
        }

        static void OpenBucket()
        {
            var bucket1 = ClusterHelper.GetBucket("default");
            TwoThreadsCompleted.Signal();
        }

        [Test]
        public void When_A_Bucket_Instance_Is_Nulled_Its_Reference_Still_Exists()
        {
            ClusterHelper.Initialize(TestConfiguration.GetCurrentConfiguration());

            var bucket1 = ClusterHelper.GetBucket("default");
            bucket1 = null;
            bucket1 = ClusterHelper.GetBucket("default");
            Assert.IsNotNull(bucket1);

        }

        [Test]
        [Category("Integration")]
        public void When_Configuration_Has_Password_For_Bucket_It_Is_Used()
        {
            //first check that without password default one is used (which should work)
            var config = TestConfiguration.GetCurrentConfiguration();

            ClusterHelper.Initialize(config);
            var bucket = ClusterHelper.GetBucket("beer-sample");
            Assert.NotNull(bucket);

            //then check that putting a password in configuration fails the same test
            ClusterHelper.RemoveBucket("beer-sample");
            config.BucketConfigs["beer-sample"] = new BucketConfiguration()
            {
                BucketName = "beer-sample",
                Password = "testpwd"
            };
            ClusterHelper.Initialize(config);

            try
            {
                bucket = ClusterHelper.GetBucket("beer-sample");
                Assert.Fail("Unexpected GetBucket success");
            }
            catch (AggregateException e)
            {
                e = e.Flatten();
                if (!(e.InnerException is AuthenticationException))
                {
                    Assert.Fail("Expected authentication exception, got " + e.InnerException);
                }
                //success
            }
        }

        [Test]
        public void When_GetBucket_Is_Called_And_NotInitialized_ThrowInitializationException()
        {
            Assert.Throws<InitializationException>(()=>ClusterHelper.GetBucket("default"));
        }

        [TearDown]
        public void TearDown()
        {
            if (_cluster != null)
            {
                _cluster.Dispose();
            }
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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