using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Utils;
using NUnit.Framework;

namespace Couchbase.Tests.Utils
{
    [TestFixture]
    public class UriExtensionTests
    {
        [Test]
        public void When_Hostname_Is_IPAddress_Return_It()
        {
            var uri = new Uri("http://192.168.56.102:8091/pools");
            var ipAddress = uri.GetIpAddress();
            Assert.AreEqual("192.168.56.102", ipAddress.ToString());
        }

        [Test]
        public void When_GetIPAddress_Called_With_LocalHost_Returns_LoopBackIP()
        {
            var uri = new Uri("http://127.0.0.1:8091/pools");
            var ipAddress = uri.GetIpAddress();
            Assert.AreEqual("127.0.0.1", ipAddress.ToString());
        }

        [Test]
        public void When_GetIpAddress_Called_With_IpAddress_Returns_HostIP()
        {
            var uri = new Uri("http://127.0.0.1:8091/pools");
            var ipAddress = uri.GetIpAddress();
            Assert.AreEqual("127.0.0.1", ipAddress.ToString());
        }

        [Test]
        public void When_GetIPEndpoint_Called_With_LocalHost_Returns_LoopBackIP()
        {
            var uri = new Uri("http://127.0.0.1:8091/pools");
            var ipEndPoint = uri.GetIPEndPoint(12101);
            Assert.AreEqual("127.0.0.1", ipEndPoint.Address.ToString());
        }

        [Test]
        public void When_GetIPEndpoint_Called_With_IpAddress_Returns_HostIP()
        {
            var uri = new Uri("http://127.0.0.1:8091/pools");
            var ipEndPoint = uri.GetIPEndPoint(12101);
            Assert.AreEqual("127.0.0.1", ipEndPoint.Address.ToString());
        }
    }
}
