using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.IO;
using Couchbase.IO.Strategies.Awaitable;
using Couchbase.Tests.Helpers;
using NUnit.Framework;

namespace Couchbase.Tests.Core.Buckets
{
    [TestFixture]
    public class KetamaMapperTests
    {
        private KetamaKeyMapper _keyMapper;
        private List<IServer> _servers;
        private VBucketServerMap _vBucketServerMap;
        
        [TestFixtureSetUp]
        public void SetUp()
        {
            var bucket = ConfigUtil.ServerConfig.Buckets.Find(x => x.BucketType == "memcached");
            _servers = bucket.Nodes.
                Select(node => new Server(ObjectFactory.CreateIOStrategy(node), node)).
                Cast<IServer>().
                ToList();

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
            //Assert.AreEqual(276, index);
            Assert.AreEqual(275, index);
        }

        [TestFixtureTearDown]
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