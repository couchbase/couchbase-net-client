using System;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.IntegrationTests.Utils;
using Couchbase.IO;
using NUnit.Framework;

namespace Couchbase.IntegrationTests.Configuration.Client
{
    [TestFixture]
    public class ConnectionStringTests
    {
        [Test]
        public async Task Test_Couchbase()
        {
            var definition = new CouchbaseClientDefinition
            {
                ConnectionString = "couchbase://" + TestConfiguration.Settings.Hostname
            };

            using (var cluster = new Cluster(definition))
            {
                cluster.SetupEnhancedAuth();

                using (var bucket = cluster.OpenBucket())
                {
                    const string key = "thekey";
                    const string value = "thevalue";

                    await bucket.RemoveAsync(key);
                    await bucket.InsertAsync(key, value);
                    var result = await bucket.GetAsync<string>(key);
                    Assert.AreEqual(ResponseStatus.Success, result.Status);
                }
            }
        }

        [Test]
        public async Task Test_Couchbases()
        {
            var definition = new CouchbaseClientDefinition
            {
                ConnectionString = "couchbases://" + TestConfiguration.Settings.Hostname,
                IgnoreRemoteCertificateNameMismatch = true
            };

            using (var cluster = new Cluster(definition))
            {
                cluster.SetupEnhancedAuth();

                using (var bucket = cluster.OpenBucket())
                {
                    const string key = "thekey";
                    const string value = "thevalue";

                    await bucket.RemoveAsync(key);
                    await bucket.InsertAsync(key, value);
                    var result = await bucket.GetAsync<string>(key);
                    Assert.AreEqual(ResponseStatus.Success, result.Status);
                }
            }
        }

        [Test]
        public async Task Test_Http()
        {
            var definition = new CouchbaseClientDefinition
            {
                ConnectionString = "http://" + TestConfiguration.Settings.Hostname
            };

            using (var cluster = new Cluster(definition))
            {
                cluster.SetupEnhancedAuth();

                using (var bucket = cluster.OpenBucket())
                {
                    const string key = "thekey";
                    const string value = "thevalue";

                    await bucket.RemoveAsync(key);
                    await bucket.InsertAsync(key, value);
                    var result = await bucket.GetAsync<string>(key);
                    Assert.AreEqual(ResponseStatus.Success, result.Status);
                }
            }
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
