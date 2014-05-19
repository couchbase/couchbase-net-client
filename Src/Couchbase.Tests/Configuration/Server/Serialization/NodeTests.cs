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
    public class NodeTests
    {
        private Node _node1;
        private Node _node2;
        private Node _node3;

        [TestFixtureSetUp]
        public void Setup()
        {
            _node1 = new Node
            {
                ClusterMembership = "active",
                Hostname = "192.168.56.104:8091",
                Status = "healthy"
            };

            _node2 = new Node
            {
                ClusterMembership = "active",
                Hostname = "192.168.56.104:8091",
                Status = "healthy"
            };

            _node3 = new Node
            {
                ClusterMembership = "active",
                Hostname = "192.168.56.101:8091",
                Status = "healthy"
            };
        }

        [Test]
        public void Test_GetHashCode()
        {
            Assert.AreEqual(_node1.GetHashCode(), _node2.GetHashCode());
            Assert.AreNotEqual(_node2.GetHashCode(), _node3.GetHashCode());
        }

        [Test]
        public void Test_Equals()
        {
            Assert.IsTrue(_node1.Equals(_node2));
            Assert.IsFalse(_node2.Equals(_node3));
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