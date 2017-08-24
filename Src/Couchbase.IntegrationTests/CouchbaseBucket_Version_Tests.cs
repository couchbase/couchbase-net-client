using System;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Version;
using Couchbase.IntegrationTests.Utils;
using NUnit.Framework;

namespace Couchbase.IntegrationTests
{
    [TestFixture]
    public class CouchbaseBucketVersionTests
    {
        private ICluster _cluster;
        private IBucket _bucket;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _cluster = new Cluster(TestConfiguration.GetCurrentConfiguration());
            _cluster.SetupEnhancedAuth();
            _bucket = _cluster.OpenBucket("default");
        }

        #region GetClusterVersion

        [Test]
        public void GetClusterVersion_ReturnsValue()
        {
            var version = _bucket.GetClusterVersion();

            Assert.IsNotNull(version);
            Assert.True(version.Value >= new ClusterVersion(new Version(1, 0, 0)));

            Console.WriteLine(version);
        }

        [Test]
        public async Task GetClusterVersionAsync_ReturnsValue()
        {
            var version = await _bucket.GetClusterVersionAsync();

            Assert.IsNotNull(version);
            Assert.True(version.Value >= new ClusterVersion(new Version(1, 0, 0)));

            Console.WriteLine(version);
        }

        #endregion

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _cluster.CloseBucket(_bucket);
            _cluster.Dispose();
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
