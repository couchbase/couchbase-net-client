using System;
using System.Linq;
using System.Security.Authentication;
using Couchbase.Configuration;
using Couchbase.Configuration.Client;
using NUnit.Framework;
using System.Collections.Generic;
using Couchbase.Tests.Utils;

namespace Couchbase.Tests.Authentication
{
    [TestFixture]
    public class BucketAuthenticationTests
    {
        [Test]
        public void When_Valid_Credentials_Provided_Bucket_Created_Succesfully()
        {
            var config = ClientConfigUtil.GetConfiguration();
            config.BucketConfigs = new Dictionary<string, BucketConfiguration>
                {
                    {
                        "authenticated",
                        new BucketConfiguration
                        {
                            BucketName = "authenticated"
                        }
                    }
                };

            var cluster = new Cluster(config);
            var bucket = cluster.OpenBucket("authenticated", "secret");
            cluster.CloseBucket(bucket);
            Assert.IsNotNull(bucket);
            cluster.CloseBucket(bucket);
        }

        /// <summary>
        /// Note that Couchbase Server returns an auth error if the bucket doesn't exist.
        /// </summary>
        [Test]
        public void When_InValid_Credentials_Provided_Bucket_Created_UnSuccesfully()
        {
            var ex = Assert.Throws<AggregateException>(() => {
                var config = ClientConfigUtil.GetConfiguration();
                config.BucketConfigs = new Dictionary<string, BucketConfiguration>
                {
                    {
                        "authenticated",
                        new BucketConfiguration
                        {
                            BucketName = "authenticated"
                        }
                    }
                };

                var cluster = new Cluster(config);
                var bucket = cluster.OpenBucket("authenticated", "secretw");
                cluster.CloseBucket(bucket);
                Assert.IsNotNull(bucket);
            });

            Assert.True(ex.InnerExceptions.OfType<AuthenticationException>().Any());
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