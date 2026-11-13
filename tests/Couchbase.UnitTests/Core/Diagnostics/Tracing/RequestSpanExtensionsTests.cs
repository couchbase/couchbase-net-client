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
    }
}
