using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.Cryptography;
using Couchbase.Tests.Fakes;
using Couchbase.Tests.Helpers;
using Couchbase.Tests.Utils;
using Couchbase.Utils;
using NUnit.Framework;

namespace Couchbase.Tests.Core.Buckets
{
    [TestFixture]
    public class VBucketKeyMapperTests
    {
        const string Key = "XXXXX";
        private Dictionary<IPAddress, IServer> _servers;
        private VBucketServerMap _vBucketServerMap;
        private IBucketConfig _bucketConfig;

        [TestFixtureSetUp]
        public void SetUp()
        {
            _bucketConfig = ConfigUtil.ServerConfig.Buckets.First();
            _vBucketServerMap = _bucketConfig.VBucketServerMap;

            _servers = new Dictionary<IPAddress, IServer>();
            foreach (var node in _bucketConfig.GetNodes())
            {
                _servers.Add(node.GetIPAddress(),
                    new Server(new FakeIOStrategy(node.GetIPEndPoint(), new FakeConnectionPool(), false),
                        node,
                        new ClientConfiguration(), _bucketConfig,
                        new FakeTranscoder()));
            }
        }

        [Test]
        public void TestMapKey()
        {
            IKeyMapper mapper = new VBucketKeyMapper(_servers, _vBucketServerMap, _bucketConfig.Rev);
            var vBucket = mapper.MapKey(Key);
            Assert.IsNotNull(vBucket);
        }

        [Test(Description = "Note, will probably only work on localhost")]
        public void Test_That_Key_XXXXX_Maps_To_VBucket_389()
        {
            const int actual = 389;
            IKeyMapper mapper = new VBucketKeyMapper(_servers, _vBucketServerMap, _bucketConfig.Rev);
            var vBucket = (IVBucket)mapper.MapKey(Key);
            Assert.AreEqual(vBucket.Index, actual);
        }

        [TestFixtureTearDown]
        public void TestFixtureTearDown()
        {
            foreach (var server in _servers.Values)
            {
                server.Dispose();
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