using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Couchbase.Configuration;
using NUnit.Framework;

namespace Couchbase.Tests
{
    [TestFixture]
    public class CouchbaseClientConfigurationTests
    {
        [Test]
        public void Test_That_Default_QueueTimeout_Is_2500ms()
        {
            var twoAndOneHalfSeconds = new TimeSpan(0, 0, 0, 0, 2500);
            var config = new CouchbaseClientConfiguration();
            Assert.AreEqual(twoAndOneHalfSeconds, config.SocketPool.QueueTimeout);
        }
    }
}