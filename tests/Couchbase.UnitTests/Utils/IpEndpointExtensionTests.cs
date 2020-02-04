using System;
using System.Collections.Generic;
using System.Net;
using Couchbase.Utils;
using Xunit;

namespace Couchbase.UnitTests.Utils
{
    public class IpEndpointExtensionTests
    {
        public static IEnumerable<object[]> EndPointTests()
        {
            yield return new object[]
                {"127.0.0.1:11207", false, new IPEndPoint(new IPAddress(new byte[] {127, 0, 0, 1}), 11207)};

            yield return new object[]
                {"[::1]:9999", false, new IPEndPoint(new IPAddress(new byte[] {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1}), 9999)};
            yield return new object[]
                {"[::1]:9999", true, new IPEndPoint(new IPAddress(new byte[] {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1}), 9999)};

            yield return new object[]
                {"localhost:11210", false, new IPEndPoint(new IPAddress(new byte[] {127, 0, 0, 1}), 11210)};
        }

        [Theory]
        [MemberData(nameof(EndPointTests))]
        public void GetEndPoint_Valid_ExpectedResult(string server, bool preferIpv6, IPEndPoint expectedResult)
        {
            // Act

            var result = IpEndPointExtensions.GetEndPoint(server, preferIpv6);

            // Assert

            Assert.Equal(expectedResult, result);
        }
    }
}
