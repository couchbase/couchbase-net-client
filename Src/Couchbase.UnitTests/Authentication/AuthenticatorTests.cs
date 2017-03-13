using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Authentication;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests.Authentication
{
    [TestFixture]
    public class AuthenticatorTests
    {
        [Test]
        public void Test_Authenticate()
        {
            //arrange
            var clusterMock = new Mock<ICluster>();
            clusterMock.Setup(x => x.OpenBucket(It.IsAny<string>())).Returns(new Mock<IBucket>().Object);
            clusterMock.Setup(x => x.Authenticate(It.IsAny<IClusterCredentials>()));
            var cluster = clusterMock.Object;

            var credentials = new ClusterCredentials
            {
                ClusterUsername = "Administrator",
                ClusterPassword = "",
                BucketCredentials = new Dictionary<string, string>
                {
                    {"default", "" },
                    {"authenticated", "secret" },
                    {"memcached", "" },
                    {"travel-sample", "wayward1" }
                }
            };

            //act
            cluster.Authenticate(credentials);
            var bucket = cluster.OpenBucket("authenticated");

            //assert
            Assert.IsNotNull(bucket);
        }

        [Test]
        public void Can_Authenticate_With_PasswordAuthenticator()
        {
            var clusterMock = new Mock<ICluster>();
            clusterMock.Setup(x => x.OpenBucket(It.IsAny<string>())).Returns(new Mock<IBucket>().Object);
            clusterMock.Setup(x => x.Authenticate(It.IsAny<IAuthenticator>()));
            var cluster = clusterMock.Object;

            var authenticator = new PasswordAuthenticator("mike", "secure123");
            cluster.Authenticate(authenticator);
            var bucket = cluster.OpenBucket("defualt");

            Assert.IsNotNull(bucket);
        }

        [Test]
        public void Can_Authenticate_With_ClassicAuthenticator()
        {
            var clusterMock = new Mock<ICluster>();
            clusterMock.Setup(x => x.OpenBucket(It.IsAny<string>())).Returns(new Mock<IBucket>().Object);
            clusterMock.Setup(x => x.Authenticate(It.IsAny<IAuthenticator>()));
            var cluster = clusterMock.Object;

            var authenticator = new ClassicAuthenticator("administrator", "password");
            cluster.Authenticate(authenticator);
            var bucket = cluster.OpenBucket("defualt");

            Assert.IsNotNull(bucket);
        }

        [Test]
        public void Legacy_Credentials_Sets_ClassicAuthenticator_Properties()
        {
            var credentials = new ClusterCredentials
            {
                ClusterUsername = "Administrator",
                ClusterPassword = "",
                BucketCredentials = new Dictionary<string, string>
                {
                    {"default", "" },
                    {"authenticated", "secret" },
                    {"memcached", "" },
                    {"travel-sample", "wayward1" }
                }
            };

            var authenticator = new ClassicAuthenticator(credentials);

            Assert.AreEqual(credentials.ClusterUsername, authenticator.ClusterUsername);
            Assert.AreEqual(credentials.ClusterPassword, authenticator.ClusterPassword);
            CollectionAssert.AreEquivalent(credentials.BucketCredentials, authenticator.BucketCredentials);
        }

        [Test]
        public void Null_Authenticator_Throws_NullArgumentException()
        {
            var cluster = new Cluster();
            Assert.Throws<ArgumentNullException>(() => cluster.Authenticate((IAuthenticator) null));
        }

        [Test]
        public void Authenticator_Uses_Username_From_ConnectionString_If_Not_Set()
        {
            var config = new ClientConfiguration
            {
                Servers = new List<Uri> {new Uri("couchbase://mike@localhost")}
            };
            var cluster = new Cluster(config);

            var authenticator = new PasswordAuthenticator("password");
            cluster.Authenticate(authenticator);

            var credentials = cluster.Configuration.GetCredentials(AuthContext.BucketKv);

            Assert.AreEqual("mike", credentials.Keys.First());
        }

        [Test]
        public void Authenticator_Created_With_Username_Doesnt_Use_Useranme_From_ConnectionString()
        {
            var config = new ClientConfiguration
            {
                Servers = new List<Uri> { new Uri("couchbase://mike@localhost") }
            };
            var cluster = new Cluster(config);

            var authenticator = new PasswordAuthenticator("test_user", "password");
            cluster.Authenticate(authenticator);

            var credentials = cluster.Configuration.GetCredentials(AuthContext.BucketKv);

            Assert.AreEqual("test_user", credentials.First().Key);
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
