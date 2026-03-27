using System;
using System.Collections.Generic;
using Couchbase.Core.Diagnostics;
using Couchbase.Core.Diagnostics.Tracing;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.Diagnostics.Tracing;

public class RequestSpanWrapperTests
{
    #region ChildSpan wrapping

    [Fact]
    public void ChildSpan_ReturnsWrappedSpan_NotRawInnerSpan()
    {
        var innerChild = new Mock<IRequestSpan>().Object;
        var innerSpan = new Mock<IRequestSpan>();
        innerSpan.Setup(s => s.ChildSpan("dispatch_to_server")).Returns(innerChild);

        var wrapper = new RequestSpanWrapper(innerSpan.Object, convention: ObservabilitySemanticConvention.Modern);
        var child = wrapper.ChildSpan("dispatch_to_server");

        Assert.IsType<RequestSpanWrapper>(child);
        Assert.NotSame(innerChild, child);
    }

    [Fact]
    public void ChildSpan_Wrapped_RoutesAttributesThroughSemanticConventionEmitter()
    {
        // Arrange: inner child span that records attribute calls
        var attributes = new List<(string Key, string Value)>();
        var innerChild = new Mock<IRequestSpan>();
        innerChild.Setup(s => s.SetAttribute(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((k, v) => attributes.Add((k, v)))
            .Returns(innerChild.Object);

        var innerSpan = new Mock<IRequestSpan>();
        innerSpan.Setup(s => s.ChildSpan(It.IsAny<string>())).Returns(innerChild.Object);

        var wrapper = new RequestSpanWrapper(innerSpan.Object, convention: ObservabilitySemanticConvention.Modern);
        var child = wrapper.ChildSpan("dispatch_to_server");

        // Act: set a mappable attribute on the child
        child.SetAttribute("db.system", "couchbase");

        // Assert: should have been mapped to the modern key
        Assert.Single(attributes);
        Assert.Equal("db.system.name", attributes[0].Key);
        Assert.Equal("couchbase", attributes[0].Value);
    }

    [Fact]
    public void ChildSpan_Wrapped_Both_EmitsLegacyAndModernAttributes()
    {
        var attributes = new List<(string Key, string Value)>();
        var innerChild = new Mock<IRequestSpan>();
        innerChild.Setup(s => s.SetAttribute(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((k, v) => attributes.Add((k, v)))
            .Returns(innerChild.Object);

        var innerSpan = new Mock<IRequestSpan>();
        innerSpan.Setup(s => s.ChildSpan(It.IsAny<string>())).Returns(innerChild.Object);

        var wrapper = new RequestSpanWrapper(innerSpan.Object, convention: ObservabilitySemanticConvention.Both);
        var child = wrapper.ChildSpan("dispatch_to_server");

        child.SetAttribute("net.peer.name", "10.0.0.1");

        Assert.Equal(2, attributes.Count);
        Assert.Equal(("net.peer.name", "10.0.0.1"), attributes[0]);
        Assert.Equal(("server.address", "10.0.0.1"), attributes[1]);
    }

    [Fact]
    public void ChildSpan_Legacy_PassesThroughWithoutMapping()
    {
        var attributes = new List<(string Key, string Value)>();
        var innerChild = new Mock<IRequestSpan>();
        innerChild.Setup(s => s.SetAttribute(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((k, v) => attributes.Add((k, v)))
            .Returns(innerChild.Object);

        var innerSpan = new Mock<IRequestSpan>();
        innerSpan.Setup(s => s.ChildSpan(It.IsAny<string>())).Returns(innerChild.Object);

        var wrapper = new RequestSpanWrapper(innerSpan.Object, convention: ObservabilitySemanticConvention.Legacy);
        var child = wrapper.ChildSpan("dispatch_to_server");

        child.SetAttribute("db.system", "couchbase");

        Assert.Single(attributes);
        Assert.Equal(("db.system", "couchbase"), attributes[0]);
    }

    #endregion

    #region Convention propagation through parent span

    [Fact]
    public void SetAttribute_Modern_MapsLegacyKeyToModern()
    {
        var attributes = new List<(string Key, string Value)>();
        var innerSpan = new Mock<IRequestSpan>();
        innerSpan.Setup(s => s.SetAttribute(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((k, v) => attributes.Add((k, v)))
            .Returns(innerSpan.Object);

        var wrapper = new RequestSpanWrapper(innerSpan.Object, convention: ObservabilitySemanticConvention.Modern);
        wrapper.SetAttribute("db.couchbase.scope", "myScope");

        Assert.Single(attributes);
        Assert.Equal("couchbase.scope.name", attributes[0].Key);
    }

    #endregion
}
