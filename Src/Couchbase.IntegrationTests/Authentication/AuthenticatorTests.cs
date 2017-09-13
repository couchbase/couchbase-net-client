using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using Castle.Core.Internal;
using Couchbase.Authentication;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Providers;
using Couchbase.Core;
using Couchbase.IntegrationTests.Utils;
using Couchbase.Management;
using Couchbase.N1QL;
using Couchbase.Search;
using Couchbase.Search.Queries.Simple;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Couchbase.IntegrationTests.Authentication
{
    [TestFixture]
    public class AuthenticatorTests
    {
        [Test]
        public void Test_CanAuthenticate_KV()
        {
            var credentials = new ClusterCredentials
            {
                ClusterUsername = "Administrator",
                ClusterPassword = "password",
                BucketCredentials = new AttributeDictionary
                {
                    {"authenticated", "secret"}
                }
            };

            var cluster = new Cluster(TestConfiguration.GetCurrentConfiguration());

            cluster.Authenticate(credentials);

            var bucket = cluster.OpenBucket("authenticated");
            var result = bucket.Upsert("thekey", "thevalue");
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void Test_CanAuthenticate_KV_HTTP()
        {
            var credentials = new ClusterCredentials
            {
                ClusterUsername = "Administrator",
                ClusterPassword = "password",
                BucketCredentials = new AttributeDictionary
                {
                    {"authenticated", "secret"}
                }
            };

            var config = TestConfiguration.GetCurrentConfiguration();
            config.ConfigurationProviders = ServerConfigurationProviders.HttpStreaming;

            var cluster = new Cluster(config);

            cluster.Authenticate(credentials);

            var bucket = cluster.OpenBucket("authenticated");
            var result = bucket.Upsert("thekey", "thevalue");
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void Test_CanAuthenticate_N1QL()
        {
            var credentials = new ClusterCredentials
            {
                ClusterUsername = "Administrator",
                ClusterPassword = "password",
                BucketCredentials = new Dictionary<string, string>
                {
                    {"authenticated", "secret"}
                }
            };

            var cluster = new Cluster(TestConfiguration.GetCurrentConfiguration());

            cluster.Authenticate(credentials);

            var bucket = cluster.OpenBucket("authenticated");

            var query = new QueryRequest("SELECT * FROM `authenticated`;");
            var result = bucket.Query<dynamic>(query);

            Assert.IsTrue(result.Success);
        }

        [Test]
        public void Test_CanAuthenticate_Views()
        {
            var credentials = new ClusterCredentials
            {
                ClusterUsername = "Administrator",
                ClusterPassword = "password",
                BucketCredentials = new AttributeDictionary
                {
                    {"authenticated", "secret"}
                }
            };

            var cluster = new Cluster(TestConfiguration.GetCurrentConfiguration());

            cluster.Authenticate(credentials);

            //if authentication failed - an exception would be thrown during bootstrapping
            var bucket = cluster.OpenBucket("authenticated");
            var query = bucket.CreateQuery("somedoc", "someview");
            var result = bucket.Query<dynamic>(query);

            //assert - view does not exist but should still return a response and no auth error
            Assert.IsFalse(result.Success);
            Assert.AreEqual(HttpStatusCode.NotFound, result.StatusCode);
        }

        [Test]
        public void Test_Legacy_CanAuthenticate_KV()
        {
            var cluster = new Cluster(TestConfiguration.GetCurrentConfiguration());

            var bucket = cluster.OpenBucket("authenticated", "secret");
            var result = bucket.Upsert("thekey", "thevalue");
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void Test_Legacy_CanAuthenticate_KV_HTTP()
        {
            var config = TestConfiguration.GetCurrentConfiguration();
            config.ConfigurationProviders = ServerConfigurationProviders.HttpStreaming;

            var cluster = new Cluster(config);

            var bucket = cluster.OpenBucket("authenticated", "secret");
            var result = bucket.Upsert("thekey", "thevalue");
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void Test_Legacy_CanAuthenticate_N1QL()
        {
            var cluster = new Cluster(TestConfiguration.GetCurrentConfiguration());

            var bucket = cluster.OpenBucket("authenticated", "secret");

            var query = new QueryRequest("SELECT * FROM `authenticated`;");
            var result = bucket.Query<dynamic>(query);

            Assert.IsTrue(result.Success);
        }

        [Test]
        public void Test_Legacy_CanAuthenticate_Views()
        {
            var cluster = new Cluster(TestConfiguration.GetCurrentConfiguration());

            //if authentication failed - an exception would be thrown during bootstrapping
            var bucket = cluster.OpenBucket("authenticated", "secret");
            var query = bucket.CreateQuery("somedoc", "someview");
            var result = bucket.Query<dynamic>(query);

            //assert - view does not exist but should still return a response and no auth error
            Assert.IsFalse(result.Success);
            Assert.AreEqual(HttpStatusCode.NotFound, result.StatusCode);
        }

        #region Password Authenticator

        private static PasswordAuthenticator GetPasswordAuthenticator()
        {
            return new PasswordAuthenticator("test_user", "secure123");
        }

        [Test]
        public void PasswordAuthenticator_KV()
        {
            TestConfiguration.IgnoreIfRbacOnly();

            var authentictor = GetPasswordAuthenticator();
            var cluster = new Cluster(TestConfiguration.GetCurrentConfiguration());
            cluster.Authenticate(authentictor);

            var bucket = cluster.OpenBucket("authenticated");
            var result = bucket.Upsert("thekey", "thevalue");
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void PasswordAuthenticator_KV_HTTP()
        {
            TestConfiguration.IgnoreIfRbacOnly();

            var authentictor = GetPasswordAuthenticator();
            var config = TestConfiguration.GetCurrentConfiguration();
            config.ConfigurationProviders = ServerConfigurationProviders.HttpStreaming;

            var cluster = new Cluster(config);
            cluster.Authenticate(authentictor);

            var bucket = cluster.OpenBucket("authenticated");
            var result = bucket.Upsert("thekey", "thevalue");
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void PasswordAuthenticator_KV_SSL()
        {
            TestConfiguration.IgnoreIfRbacOnly();

            var authentictor = GetPasswordAuthenticator();
            var config = TestConfiguration.GetCurrentConfiguration();
            config.UseSsl = true;

            ClientConfiguration.IgnoreRemoteCertificateNameMismatch = true;

            var cluster = new Cluster(config);
            cluster.Authenticate(authentictor);

            var bucket = cluster.OpenBucket("authenticated");
            var result = bucket.Upsert("thekey", "thevalue");
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void PasswordAuthenticator_N1QL()
        {
            TestConfiguration.IgnoreIfRbacOnly();

            var authentictor = GetPasswordAuthenticator();
            var cluster = new Cluster(TestConfiguration.GetCurrentConfiguration());

            cluster.Authenticate(authentictor);

            var bucket = cluster.OpenBucket("authenticated");

            var query = new QueryRequest("SELECT * FROM `authenticated`;");
            var result = bucket.Query<dynamic>(query);

            Assert.IsTrue(result.Success);
        }

        [Test]
        public void PasswordAuthenticator_Views()
        {
            TestConfiguration.IgnoreIfRbacOnly();

            var authentictor = GetPasswordAuthenticator();
            var cluster = new Cluster(TestConfiguration.GetCurrentConfiguration());
            cluster.Authenticate(authentictor);

            var bucket = cluster.OpenBucket("authenticated");
            var query = bucket.CreateQuery("somedoc", "someview");
            var result = bucket.Query<dynamic>(query);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(HttpStatusCode.NotFound, result.StatusCode);
        }

        [Test]
        public void PasswordAuthenticator_FTS()
        {
            TestConfiguration.IgnoreIfRbacOnly();

            var authenticator = GetPasswordAuthenticator();
            var cluster = new Cluster(TestConfiguration.GetCurrentConfiguration());
            cluster.Authenticate(authenticator);

            var bucket = cluster.OpenBucket("authenticated");
            var query = new SearchQuery
            {
                Index = "hotel",
                Query = new PhraseQuery("inn").Field("name")
            };

            var result = bucket.Query(query);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(SearchStatus.Success, result.Status);
        }

        [Test]
        public void PasswordAuthenticator_ClusterManager()
        {
            TestConfiguration.IgnoreIfRbacOnly();

            var authenticator = GetPasswordAuthenticator();
            var cluster = new Cluster(TestConfiguration.GetCurrentConfiguration());
            cluster.Authenticate(authenticator);

            var clusterManager = cluster.CreateManager();
            var listbucketsResult = clusterManager.ListBuckets();

            Assert.IsNotNull(clusterManager);
            Assert.IsTrue(listbucketsResult.Success);
            Assert.IsTrue(listbucketsResult.Value.Any());
        }

        [Test]
        public void PasswordAuthenticator_BucketManager()
        {
            TestConfiguration.IgnoreIfRbacOnly();

            var authenticator = GetPasswordAuthenticator();
            var cluster = new Cluster(TestConfiguration.GetCurrentConfiguration());
            cluster.Authenticate(authenticator);

            var bucket = cluster.OpenBucket("authenticated");
            var bucketManager = bucket.CreateManager();

            var listIndexesResult = bucketManager.ListN1qlIndexes();

            Assert.IsTrue(listIndexesResult.Success);
        }

        [Test]
        public void PasswordAuthenticator_N1QL_Cluster()
        {
            TestConfiguration.IgnoreIfRbacOnly();

            var authenticator = GetPasswordAuthenticator();
            var cluster = new Cluster(TestConfiguration.GetCurrentConfiguration());
            cluster.Authenticate(authenticator);

            // force bucket open before sumbitting query
            cluster.OpenBucket("authenticated");

            var query = new QueryRequest("SELECT * FROM `authenticated`;");
            var result = cluster.Query<dynamic>(query);

            Assert.IsTrue(result.Success);
        }

        [Test]
        public void PasswordAuthenticator_Can_Connect_With_Username_From_ConnectionString()
        {
            TestConfiguration.IgnoreIfRbacOnly();

            const string username = "test_user";
            var config = TestConfiguration.GetDefaultConfiguration();

            // update server uri's to add the username segment
            config.Servers = config.Servers.Select(x => InsertUsernameIntoUri(x, username)).ToList();

            // create authenticator without username
            var passwordAuthenticator = new PasswordAuthenticator("secure123");
            var cluster = new Cluster(config);
            cluster.Authenticate(passwordAuthenticator);

            // perform KV operation
            var bucket = cluster.OpenBucket("authenticated");
            var result = bucket.Upsert("thekey", "thevalue");
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void PasswordAuthenticator_When_Password_Invalid_Return_Error_Msg()
        {
            TestConfiguration.IgnoreIfRbacOnly();

            var config = TestConfiguration.GetDefaultConfiguration();

            // create authenticator without username
            var passwordAuthenticator = new PasswordAuthenticator("default", "badpassword");
            var cluster = new Cluster(config);
            cluster.Authenticate(passwordAuthenticator);

            // perform KV operation
            try
            {
                var bucket = cluster.OpenBucket("default");
            }
            catch (AggregateException e)
            {
                var exp = e.Flatten().InnerExceptions;
                Assert.AreEqual("Authentication failed for user 'default'", exp[1].Message);
            }
        }

        [Test]
        public void PasswordAuthenticator_When_User_Does_Not_Have_Access_Return_Error_Message()
        {
            TestConfiguration.IgnoreIfRbacOnly();

            var config = TestConfiguration.GetDefaultConfiguration();

            // create authenticator without username
            var cluster = new Cluster(config);
            cluster.SetupEnhancedAuth();

            // perform KV operation
            try
            {
                var bucket = cluster.OpenBucket("doesnotexist");
            }
            catch (AggregateException e)
            {
                var exp = e.Flatten().InnerExceptions;
                Assert.AreEqual(exp[1].Message, "Authentication failed for bucket 'doesnotexist'");
            }
        }

        [Test]
        public void PasswordAuthenticator_Memcached_KV()
        {
            TestConfiguration.IgnoreIfRbacOnly();

            var authentictor = GetPasswordAuthenticator();
            var cluster = new Cluster(TestConfiguration.GetCurrentConfiguration());
            cluster.Authenticate(authentictor);

            var bucket = cluster.OpenBucket("memcached");
            var result = bucket.Upsert("thekey", "thevalue");
            Assert.IsTrue(result.Success);
        }

#if NET45
        [Test]
        public void PasswordAuthenticator_Can_Auth_Using_ConfigSection()
        {
            TestConfiguration.IgnoreIfRbacOnly();

            var cluster = new Cluster(configurationSectionName: "couchbaseClients/basic");
            var auth = new PasswordAuthenticator("test_user", "secure123");
            cluster.Authenticate(auth);
            Assert.IsNotNull(cluster.OpenBucket("authenticated"));
        }

        [Test]
        public void Creates_Password_Authenticator_Using_Username_Password_From_Config()
        {
            TestConfiguration.IgnoreIfRbacOnly();

            var cluster = new Cluster(configurationSectionName: "couchbaseClients/secure");

            // test the password authenticator is setup correctly
            var authenticator = cluster.Configuration.Authenticator as PasswordAuthenticator;
            Assert.IsNotNull(authenticator);
            Assert.AreEqual("CustomUser", authenticator.Username);
            Assert.AreEqual("secure123", authenticator.Password);

            // make sure you can open a bucket
            Assert.IsNotNull(cluster.OpenBucket("default"));
        }

        [Test]
        public void Creates_Password_Authenticator_Using_Password_From_Config_And_Username_From_ConnectionString()
        {
            TestConfiguration.IgnoreIfRbacOnly();

            var cluster = new Cluster(configurationSectionName: "couchbaseClients/secureConnectionString");

            // test the password authenticator is setup correctly
            var authenticator = cluster.Configuration.Authenticator as PasswordAuthenticator;
            Assert.IsNotNull(authenticator);
            Assert.AreEqual("CustomUser", authenticator.Username);
            Assert.AreEqual("secure123", authenticator.Password);

            // make sure you can open a bucket
            Assert.IsNotNull(cluster.OpenBucket("default"));
        }
#endif
        #endregion

        #region ClassicAuthenticator

        private static IAuthenticator GetClassicAuthenticator()
        {
            var authenticator = new ClassicAuthenticator("Administrator", "password");
            authenticator.AddBucketCredential("authenticated", "secret");
            return authenticator;
        }

        [Test]
        public void ClassicAuthenticator_KV()
        {
            var authenticator = GetClassicAuthenticator();
            var cluster = new Cluster(TestConfiguration.GetCurrentConfiguration());
            cluster.Authenticate(authenticator);

            var bucket = cluster.OpenBucket("authenticated");
            var result = bucket.Upsert("thekey", "thevalue");
            Assert.IsTrue(result.Success);
        }


        [Test, Ignore("Requires SSL cert to be configured.")]
        public void ClassicAuthenticator_KV_SSL()
        {
            var authenticator = GetClassicAuthenticator();
            var config = TestConfiguration.GetCurrentConfiguration();
            config.UseSsl = true;

            //disable for testing
            ClientConfiguration.IgnoreRemoteCertificateNameMismatch = true;

            var cluster = new Cluster(config);
            cluster.Authenticate(authenticator);

            var bucket = cluster.OpenBucket("authenticated");
            var result = bucket.Upsert("thekey", "thevalue");
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void ClassicAuthenticator_N1QL()
        {
            var authenticator = GetClassicAuthenticator();

            var cluster = new Cluster(TestConfiguration.GetCurrentConfiguration());
            cluster.Authenticate(authenticator);

            var bucket = cluster.OpenBucket("authenticated");

            var query = new QueryRequest("SELECT * FROM `authenticated`;");
            var result = bucket.Query<dynamic>(query);

            Assert.IsTrue(result.Success);
        }

        [Test]
        public void ClassicAuthenticator_Views()
        {
            var authenticator = GetClassicAuthenticator();

            var cluster = new Cluster(TestConfiguration.GetCurrentConfiguration());
            cluster.Authenticate(authenticator);

            //if authentication failed - an exception would be thrown during bootstrapping
            var bucket = cluster.OpenBucket("authenticated");
            var query = bucket.CreateQuery("somedoc", "someview");
            var result = bucket.Query<dynamic>(query);

            //assert - view does not exist but should still return a response and no auth error
            Assert.IsFalse(result.Success);
            Assert.AreEqual(HttpStatusCode.NotFound, result.StatusCode);
        }

        [Test]
        public void ClassicAuthenticator_Can_Create_ClusterManager_With_Username_From_ConnectionString()
        {
            const string username = "Administrator";
            var config = TestConfiguration.GetDefaultConfiguration();

            // update server uri's to add the username segment
            config.Servers = config.Servers.Select(x => InsertUsernameIntoUri(x, username)).ToList();

            // create authenticator without username
            var authenticator = new ClassicAuthenticator("password");
            var cluster = new Cluster(config);
            cluster.Authenticate(authenticator);

            var clusterManager = cluster.CreateManager();
            var listbucketsResult = clusterManager.ListBuckets();

            Assert.IsNotNull(clusterManager);
            Assert.IsTrue(listbucketsResult.Success);
            Assert.IsTrue(listbucketsResult.Value.Any());
        }

#if NET45
        [Test]
        public void ClassicAuthenticator_Can_Auth_Using_ConfigSection()
        {
            var cluster = new Cluster(configurationSectionName: "couchbaseClients/basic");
            var auth = new ClassicAuthenticator
            {
                BucketCredentials = {{"authenticated", "secret"}}
            };
            cluster.Authenticate(auth);
            Assert.IsNotNull(cluster.OpenBucket("authenticated"));
        }
#endif
        #endregion

        [Test]
        public void Data_Reader_Cannot_Write()
        {
            TestConfiguration.IgnoreIfRbacOnly();
            const string username = "reader_user",
                password = "secure123",
                bucketName = "default",
                key = "test-key";

            using (var cluster = new Cluster(TestConfiguration.GetCurrentConfiguration()))
            {
                var clusterManager = cluster.CreateManager(TestConfiguration.Settings.AdminUsername, TestConfiguration.Settings.AdminPassword);

                try
                {
                    // create user with write only permissions
                    var upsertUserResult = clusterManager.UpsertUser(
                        AuthenticationDomain.Local,
                        username,
                        password,
                        roles: new Role { BucketName = bucketName, Name = "data_reader" }
                    );
                    Assert.IsTrue(upsertUserResult.Success);

                    // authenticate with new credentials and open bucket
                    cluster.Authenticate(username, password);
                    var bucket = cluster.OpenBucket(bucketName);

                    // test getting a document
                    var getResult = bucket.Get<dynamic>(key);
                    Assert.IsTrue(getResult.Success);

                    // test lookup via subdoc
                    var lookupResult = bucket.LookupIn<dynamic>(key)
                        .Get("goodbye")
                        .Execute();
                    Assert.IsTrue(lookupResult.Success);

                    // test writing a document - should fail
                    var upsertResult = bucket.Upsert(key, new { });
                    Assert.IsFalse(upsertResult.Success);

                    // test mutating via sub doc - should fail
                    var mutateResult = bucket.MutateIn<dynamic>(key)
                        .Upsert("goodbye", "world", SubdocPathFlags.CreatePath)
                        .Execute();
                    Assert.IsFalse(mutateResult.Success);
                }
                finally
                {
                    // remove test user
                    clusterManager.RemoveUser(AuthenticationDomain.Local, username);
                }
            }
        }

        [Test]
        public void Data_Writer_Cannot_Read()
        {
            TestConfiguration.IgnoreIfRbacOnly();
            const string username = "writer_user",
                password = "secure123",
                bucketName = "default",
                key = "test-key";

            using (var cluster = new Cluster(TestConfiguration.GetCurrentConfiguration()))
            {
                var clusterManager = cluster.CreateManager(TestConfiguration.Settings.AdminUsername, TestConfiguration.Settings.AdminPassword);

                try
                {
                    // create user with write only permissions
                    var upsertUserResult = clusterManager.UpsertUser(
                        AuthenticationDomain.Local,
                        username,
                        password,
                        roles: new Role {BucketName = bucketName, Name = "data_writer"}
                    );
                    Assert.IsTrue(upsertUserResult.Success);

                    // authenticate with new credentials and open bucket
                    cluster.Authenticate(username, password);
                    var bucket = cluster.OpenBucket(bucketName);

                    // test writing a document
                    var upsertResult = bucket.Upsert(key, new { });
                    Assert.IsTrue(upsertResult.Success);

                    // test mutating via subdoc
                    var mutateResult = bucket.MutateIn<dynamic>(key)
                        .Upsert("goodbye", "world", SubdocPathFlags.CreatePath)
                        .Execute();
                    Assert.IsTrue(mutateResult.Success);

                    // test reading a document - should fail
                    var getResult = bucket.Get<dynamic>(key);
                    Assert.IsFalse(getResult.Success);

                    // test lookup via subdoc - should fail
                    var lookupResult = bucket.LookupIn<dynamic>(key)
                        .Get("goodbye")
                        .Execute();
                    Assert.IsFalse(lookupResult.Success);
                }
                finally
                {
                    // remove test user
                    clusterManager.RemoveUser(AuthenticationDomain.Local, username);
                }
            }
        }

        private static Uri InsertUsernameIntoUri(Uri uri, string username)
        {
            var index = uri.OriginalString.IndexOf("://", StringComparison.OrdinalIgnoreCase);
            return index > 0
                ? new Uri(string.Format("{0}://{1}@{2}", uri.OriginalString.Substring(0, index), username, uri.OriginalString.Substring(index + 3)))
                : uri;
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
