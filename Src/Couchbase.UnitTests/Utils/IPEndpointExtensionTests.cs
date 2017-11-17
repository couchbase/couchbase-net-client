using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using Couchbase.Utils;
using NUnit.Framework;

namespace Couchbase.UnitTests.Utils
{
    [TestFixture]
    public class IPEndpointExtensionTests
    {
        [TestFixture]
        // ReSharper disable once InconsistentNaming
        public class IPEndpointExtensionsTests
        {
            [Test]
            public void When_Hostname_Is_Provided_IPAdress_Is_Returned()
            {
                var hostname = "127.0.0.1:8091";
                var expected = "127.0.0.1";
                var actual = IPEndPointExtensions.GetEndPoint(hostname);
                Assert.AreEqual(expected, actual.Address.ToString());
            }

            [Test]
            [TestCase("::1", AddressFamily.InterNetworkV6)]
            [TestCase("[fd63:6f75:6368:2068:c8e3:a1ff:fed9:38ca]:11210", AddressFamily.InterNetworkV6)]
            [TestCase("fd63:6f75:6368:2068:c8e3:a1ff:fed9:38ca", AddressFamily.InterNetworkV6)]
            [TestCase("127.0.0.1:10210", AddressFamily.InterNetwork)]
            [TestCase("10.111.170.102:8091", AddressFamily.InterNetwork)]
            public void Test_IPv6(string ip, AddressFamily addressFamily)
            {
                var endPoint = IPEndPointExtensions.GetEndPoint(ip);

                Assert.AreEqual(addressFamily, endPoint.AddressFamily);
            }
        }
    }
}

#region [ License information          ]
/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
