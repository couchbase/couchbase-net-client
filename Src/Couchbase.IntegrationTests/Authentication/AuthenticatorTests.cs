using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using Couchbase.Authentication;
using Couchbase.Configuration.Client;
using Couchbase.IntegrationTests.Utils;
using Couchbase.N1QL;
using Couchbase.Views;
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
