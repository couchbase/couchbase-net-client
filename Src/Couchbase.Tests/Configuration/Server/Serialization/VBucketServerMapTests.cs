using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Server.Serialization;
using NUnit.Framework;

namespace Couchbase.Tests.Configuration.Server.Serialization
{
    [TestFixture]
    public class VBucketServerMapTests
    {
        private VBucketServerMap _vBucketServerMap1;
        private VBucketServerMap _vBucketServerMap2;
        private VBucketServerMap _vBucketServerMap3;

        [TestFixtureSetUp]
        public void Setup()
        {
            _vBucketServerMap1 = new VBucketServerMap
            {
                HashAlgorithm = "CRC",
                NumReplicas = 1,
                ServerList = new[] { "192.168.56.101:11210", "192.168.56.104:11210" },
                VBucketMap = new int[][] { new[] { 1, 0 }, new[] { 1, 0 }, new[] { 1, 0 } }
            };

            _vBucketServerMap2 = new VBucketServerMap
            {
                HashAlgorithm = "CRC",
                NumReplicas = 1,
                ServerList = new[] { "192.168.56.101:11210", "192.168.56.104:11210" },
                VBucketMap = new int[][] { new[] { 1, 0 }, new[] { 1, 0 }, new[] { 1, 0 } }
            };

            _vBucketServerMap3 = new VBucketServerMap
            {
                HashAlgorithm = "CRC",
                NumReplicas = 1,
                ServerList = new[] { "192.168.56.101:11210", "192.168.56.103:11210" },
                VBucketMap = new int[][] { new[] { 0, 1 }, new[] { 1, 0 }, new[] { 1, 0 } }
            };
        }

        [Test]
        public void Test_GetHashcode()
        {
            Assert.AreEqual(_vBucketServerMap1.GetHashCode(), _vBucketServerMap2.GetHashCode());
            Assert.AreNotEqual(_vBucketServerMap1.GetHashCode(), _vBucketServerMap3.GetHashCode());
        }

        [Test]
        public void Test_Equals()
        {
            Assert.IsTrue(_vBucketServerMap1.Equals(_vBucketServerMap2));
            Assert.IsFalse(_vBucketServerMap1.Equals(_vBucketServerMap3));
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