using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Authentication;
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
