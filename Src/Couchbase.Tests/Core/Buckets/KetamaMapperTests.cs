using System.Collections.Generic;
using System.Net;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.Tests.Fakes;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.Tests.Core.Buckets
{
    [TestFixture]
    public class KetamaMapperTests
    {
        private KetamaKeyMapper _keyMapper;
        private Dictionary<IPEndPoint, IServer> _servers;

        [OneTimeSetUp]
        public void SetUp()
        {
            var json = ResourceHelper.ReadResource(@"Data\Configuration\cb4-config-4-nodes.json");
            var bucketConfig = JsonConvert.DeserializeObject<BucketConfig>(json);

            _servers = new Dictionary<IPEndPoint, IServer>();
            foreach (var node in bucketConfig.GetNodes())
            {
                _servers.Add(new IPEndPoint(node.GetIPAddress(), 8091),
                    new Server(new FakeIOService(node.GetIPEndPoint(), new FakeConnectionPool(), false),
                       node,
                       new FakeTranscoder(),
                        ContextFactory.GetMemcachedContext(bucketConfig)));
            }

            _keyMapper = new KetamaKeyMapper(_servers);
        }

        [Test]
        public void Test_MapKey()
        {
            const string key = "foo";
            var node = _keyMapper.MapKey(key);
            Assert.IsNotNull(node);
        }

        [Test]
        public void Test_CalculateHash()
        {
            const string key = "foo";
            var hash = _keyMapper.GetHash(key);
            const uint expected = 3675831724;
            //3675831724
            Assert.AreEqual(hash, expected);
        }

        [Test]
        public void Test_GetIndex()
        {
            const string key = "foo";
            var hash = _keyMapper.GetHash(key);
            var index = _keyMapper.FindIndex(hash);
            Assert.AreEqual(263, index);
        }

        [OneTimeTearDown]
        public void TearDown()
        {

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