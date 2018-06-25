using System;
using System.Linq;
using System.Security.Authentication;
using System.Threading;
using Couchbase.Authentication;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.IntegrationTests.Utils;
using NUnit.Framework;

namespace Couchbase.IntegrationTests
{
    [TestFixture]
    public class ClusterHelperTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            TestConfiguration.IgnoreIfMock();
        }

        [Test]
        public void When_Get_Called_Without_Calling_Initialize_InitializationException_Is_Thrown()
        {
            ClusterHelper.Close();
            Assert.Throws<InitializationException>(() => ClusterHelper.Get());
        }

        [Test]
        public void Test_OpenBucket()
        {
            ClusterHelper.Initialize(TestConfiguration.GetCurrentConfiguration());
            var cluster = ClusterHelper.Get();
            cluster.SetupEnhancedAuth();

            var bucket = cluster.OpenBucket();
            Assert.AreEqual("default", bucket.Name);
        }

        [Test]
        public void Test_GetBucket_Using_HttpStreamingProvider()
        {
            ClusterHelper.Initialize(TestConfiguration.GetCurrentConfiguration());
            var cluster = ClusterHelper.Get();
            cluster.SetupEnhancedAuth();

            const string expected = "default";
            using (var bucket = cluster.OpenBucket("default"))
            {
                Assert.AreEqual(expected, bucket.Name);
            }
        }

        [Test]
        public void Test_GetBucket_Using_CarrierPublicationProvider()
        {
            ClusterHelper.Initialize(TestConfiguration.GetCurrentConfiguration());
            var cluster = ClusterHelper.Get();
            cluster.SetupEnhancedAuth();

            const string expected = "default";
            using (var bucket = cluster.OpenBucket("default"))
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
            cluster.SetupEnhancedAuth();

            Assert.IsNotNull(cluster);
            cluster.Dispose();
        }

        [Test]
        public void When_OpenBucket_Is_Called_Multiple_Times_Same_Bucket_Object_IsReturned()
        {
            ClusterHelper.Initialize(TestConfiguration.GetCurrentConfiguration());
            var cluster = ClusterHelper.Get();
            cluster.SetupEnhancedAuth();

            var bucket1 = cluster.OpenBucket("default");
            var bucket2 = cluster.OpenBucket("default");

            Assert.AreSame(bucket1, bucket2);
        }

        [Test]
        public void When_GetBucket_Is_Called_Multiple_Times_Same_Bucket_Object_IsReturned()
        {
            ClusterHelper.Initialize(TestConfiguration.GetCurrentConfiguration());
            ClusterHelper.Get().SetupEnhancedAuth();

            var bucket1 = ClusterHelper.GetBucket("default");
            var bucket2 = ClusterHelper.GetBucket("default");

            Assert.AreEqual(bucket1, bucket2);
        }

        [Test]
        public void When_Close_Called_Bucket_Count_Is_Zero()
        {
            ClusterHelper.Initialize(TestConfiguration.GetCurrentConfiguration());
            ClusterHelper.Get().SetupEnhancedAuth();

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
            ClusterHelper.Get().SetupEnhancedAuth();

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
            ClusterHelper.Get().SetupEnhancedAuth();

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
            try
            {
                var bucket1 = ClusterHelper.GetBucket("default");
            }
            catch (Exception e) { Console.WriteLine(e); }
            finally { TwoThreadsCompleted.Signal(); }
        }

        [Test]
        public void When_A_Bucket_Instance_Is_Nulled_Its_Reference_Still_Exists()
        {
            ClusterHelper.Initialize(TestConfiguration.GetCurrentConfiguration());
            ClusterHelper.Get().SetupEnhancedAuth();

            var bucket1 = ClusterHelper.GetBucket("default");
            bucket1 = null;
            bucket1 = ClusterHelper.GetBucket("default");
            Assert.IsNotNull(bucket1);

        }

        [Test]
        [Category("Integration")]
        public void When_Configuration_Has_Password_For_Bucket_It_Is_Used()
        {
            if (TestConfiguration.Settings.EnhancedAuth)
            {
                Assert.Ignore("BucketConfigurtions cannot be used with Server 5.0+");
            }

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
                var exceptions = e.Flatten();
                Assert.IsTrue(exceptions.InnerExceptions.OfType<AuthenticationException>().Any(), "Expected authentication exception");
            }
        }

        [Test]
        public void When_GetBucket_Is_Called_And_NotInitialized_ThrowInitializationException()
        {
            Assert.Throws<InitializationException>(()=>ClusterHelper.GetBucket("default"));
        }

        [Test]
        public void When_Authenticator_Is_Set_It_Is_Used()
        {
            if (TestConfiguration.Settings.EnhancedAuth)
            {
                Assert.Ignore("ClassicAuthenticator does not work with Server 5.0+");
            }

            try
            {
                var config = TestConfiguration.GetDefaultConfiguration();
                config.SetAuthenticator(new ClassicAuthenticator
                {
                    BucketCredentials =
                    {
                        {"authenticated", "secret"}
                    }
                });

                ClusterHelper.Initialize(config);

                var bucket = ClusterHelper.GetBucket("authenticated");
                Assert.IsNotNull(bucket);
                Assert.AreEqual("authenticated", bucket.Name);
            }
            finally
            {
                ClusterHelper.Close();
            }
        }

        [Test]
        public void Initialize_Using_PasswordAuthenticator()
        {
            if (!TestConfiguration.Settings.EnhancedAuth)
            {
                Assert.Ignore("PasswordAuthenticator requires CB 5.0 or greater.");
            }

            try
            {
                var config = TestConfiguration.GetDefaultConfiguration();
                var authenticator = new PasswordAuthenticator(TestConfiguration.Settings.AdminUsername, TestConfiguration.Settings.AdminPassword);

                ClusterHelper.Initialize(config, authenticator);
                var bucket = ClusterHelper.GetBucket("default");

                Assert.IsNotNull(bucket);
                Assert.AreEqual("default", bucket.Name);
            }
            finally
            {
                ClusterHelper.Close();
            }
        }

        [Test]
        public void SetAuthenticator_Using_PasswordAuthenticator()
        {
            if (!TestConfiguration.Settings.EnhancedAuth)
            {
                Assert.Ignore("PasswordAuthenticator requires CB 5.0 or greater.");
            }

            try
            {
                var config = TestConfiguration.GetDefaultConfiguration();
                var authenticator = new PasswordAuthenticator(TestConfiguration.Settings.AdminUsername, TestConfiguration.Settings.AdminPassword);
                config.SetAuthenticator(authenticator);

                ClusterHelper.Initialize(config);
                var bucket = ClusterHelper.GetBucket("default");

                Assert.IsNotNull(bucket);
                Assert.AreEqual("default", bucket.Name);
            }
            finally
            {
                ClusterHelper.Close();
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (ClusterHelper.Initialized)
            {
                ClusterHelper.Close();
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
