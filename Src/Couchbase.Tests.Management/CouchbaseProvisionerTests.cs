using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Couchbase.Authentication;
using Couchbase.Configuration.Client;
using Couchbase.Core.Buckets;
using Couchbase.Management;
using NUnit.Framework;

namespace Couchbase.Tests.Management
{
    [TestFixture]
    public class CouchbaseProvisionerTests
    {
        [Test]
        public async void Test_Creating_A_Cluster()
        {
            var config = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri("http://192.168.77.101:8091/"),
                    new Uri("http://192.168.77.102:8091/"),
                    new Uri("http://192.168.77.103:8091/"),
                    new Uri("http://192.168.77.104:8091/")
                }
            };

            var cluster = new Cluster(config);
            var provisioner = new ClusterProvisioner(cluster, "Administrator", "password");
           /* var results = await provisioner.ProvisionEntryPointAsync();
            foreach (var res in results.Results)
            {
                Console.WriteLine(res.Message);
            }
            Assert.IsTrue(results.Success);

            var result = await provisioner.ProvisionSampleBucketAsync("beer-sample");
            Assert.IsTrue(result.Success);

            result = await provisioner.ProvisionSampleBucketAsync("travel-sample");
            Assert.IsTrue(result.Success);

            result = await provisioner.ProvisionBucketAsync(new BucketSettings
                {
                    Name = "authenticated",
                    SaslPassword = "secret",
                    AuthType = AuthType.Sasl,
                    BucketType = BucketTypeEnum.Couchbase
                });
            Assert.IsTrue(result.Success);

            result = await provisioner.ProvisionBucketAsync(new BucketSettings
                {
                    Name = "memcached",
                    SaslPassword = "",
                    AuthType = AuthType.Sasl,
                    BucketType = BucketTypeEnum.Memcached
                });
            Assert.IsTrue(result.Success);*/

             /*var results = await provisioner.ProvisionNodesAsync(CouchbaseService.Index,
                CouchbaseService.KV,
                CouchbaseService.N1QL);

            foreach (var res in results.Results)
            {
                Console.WriteLine(res.Message);
            }*/

            var result = await provisioner.RebalanceClusterAsync();
            Console.WriteLine(result.Message);

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
