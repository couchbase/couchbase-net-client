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
