using System;
using Couchbase.Core.Diagnostics.Tracing;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.Diagnostics.Tracing
{
    public class RequestSpanExtensionsTests
    {
        // RFC 67: the attributes hold the address, not URI syntax, so an IPv6 address has no brackets.
        [Fact]
        public void Http_Addresses_Of_An_IPv6_Node_Have_No_Brackets()
        {
            var span = new Mock<IRequestSpan>();
            span.SetupGet(s => s.CanWrite).Returns(true);
            var uri = new Uri("http://[fd00::1]:8093/query/service");

            span.Object.WithRemoteAddress(uri).WithPeerAddress(uri);

            span.Verify(s => s.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.RemoteHostname, "fd00::1"), Times.Once());
            span.Verify(s => s.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.NetworkPeerAddress, "fd00::1"), Times.Once());
        }

        // RFC 67: network.peer.address is the IP address of the socket. HttpClient resolves a hostname itself, so the
        // SDK does not know that IP address, and must not put the hostname there (NCBC-4419).
        [Theory]
        [InlineData("http://10.0.0.1:8093/query/service", "10.0.0.1")]
        [InlineData("http://[fd00::1]:8093/query/service", "fd00::1")]
        [InlineData("http://node1.example.com:8093/query/service", null)]
        [InlineData("http://localhost:8093/query/service", null)]
        public void Peer_Address_Is_Set_Only_For_An_IP_Address(string uri, string expectedAddress)
        {
            var span = new Mock<IRequestSpan>();
            span.SetupGet(s => s.CanWrite).Returns(true);

            span.Object.WithPeerAddress(new Uri(uri));

            if (expectedAddress is null)
            {
                span.Verify(s => s.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.NetworkPeerAddress, It.IsAny<string>()), Times.Never());
            }
            else
            {
                span.Verify(s => s.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.NetworkPeerAddress, expectedAddress), Times.Once());
            }

            span.Verify(s => s.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.NetworkPeerPort, "8093"), Times.Once());
        }
    }
}
