using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core;

public class ClusterContextNodeExclusionTests
{
    [Fact]
    public void GetRandomNodeForService_Null_ExcludedNodes_Returns_Any_Matching_Node()
    {
        var context = CreateContext();
        var nodeA = AddQueryNode(context, "http://a:8093");
        var nodeB = AddQueryNode(context, "http://b:8093");

        var picked = context.GetRandomNodeForService(ServiceType.Query, null, excludedNodes: null);

        Assert.NotNull(picked);
        Assert.Contains(picked, new[] { nodeA, nodeB });
    }

    [Fact]
    public void GetRandomNodeForService_Empty_ExcludedNodes_Returns_Any_Matching_Node()
    {
        var context = CreateContext();
        var nodeA = AddQueryNode(context, "http://a:8093");
        var nodeB = AddQueryNode(context, "http://b:8093");

        var picked = context.GetRandomNodeForService(ServiceType.Query, null, new List<Uri>());

        Assert.NotNull(picked);
        Assert.Contains(picked, new[] { nodeA, nodeB });
    }

    [Fact]
    public void GetRandomNodeForService_Excludes_Listed_Uris()
    {
        var context = CreateContext();
        var uriA = new Uri("http://a:8093");
        var uriB = new Uri("http://b:8093");
        AddQueryNode(context, uriA);
        var nodeB = AddQueryNode(context, uriB);

        // Run several times — randomness shouldn't ever pick the excluded node
        for (var i = 0; i < 20; i++)
        {
            var picked = context.GetRandomNodeForService(ServiceType.Query, null, new List<Uri> { uriA });
            Assert.Same(nodeB, picked);
        }
    }

    [Fact]
    public void GetRandomNodeForService_All_Nodes_Excluded_Falls_Back_To_Any_Matching_Node()
    {
        // When every node with the service is in the exclusion list, the helper
        // falls back to returning any matching node rather than null — better to
        // retry a flapping node than throw ServiceNotAvailable.
        var context = CreateContext();
        var uriA = new Uri("http://a:8093");
        var uriB = new Uri("http://b:8093");
        var nodeA = AddQueryNode(context, uriA);
        var nodeB = AddQueryNode(context, uriB);

        var picked = context.GetRandomNodeForService(
            ServiceType.Query,
            null,
            new List<Uri> { uriA, uriB });

        Assert.NotNull(picked);
        Assert.Contains(picked, new[] { nodeA, nodeB });
    }

    [Fact]
    public void GetRandomNodeForService_Search_Honors_Exclusion()
    {
        var context = CreateContext();
        var uriA = new Uri("http://a:8094");
        var uriB = new Uri("http://b:8094");
        AddSearchNode(context, uriA);
        var nodeB = AddSearchNode(context, uriB);

        var picked = context.GetRandomNodeForService(ServiceType.Search, null, new List<Uri> { uriA });

        Assert.Same(nodeB, picked);
    }

    [Fact]
    public void GetRandomNodeForService_Excluding_QueryUri_Does_Not_Affect_Search_On_Same_Node()
    {
        // A node hosting both query and search; excluding its Query URI shouldn't
        // hide it from a search lookup because the URIs are compared per-service.
        var context = CreateContext();
        var queryUri = new Uri("http://a:8093");
        var searchUri = new Uri("http://a:8094");

        var node = new Mock<IClusterNode>();
        node.SetupGet(x => x.HasQuery).Returns(true);
        node.SetupGet(x => x.HasSearch).Returns(true);
        node.SetupGet(x => x.QueryUri).Returns(queryUri);
        node.SetupGet(x => x.SearchUri).Returns(searchUri);
        context.AddNode(node.Object);

        var picked = context.GetRandomNodeForService(ServiceType.Search, null, new List<Uri> { queryUri });

        Assert.Same(node.Object, picked);
    }

    [Fact]
    public void GetRandomNodeForService_Backwards_Compat_Overload_Still_Works()
    {
        // The original signature (no exclusion list) delegates to the new one with null.
        var context = CreateContext();
        var node = AddQueryNode(context, "http://a:8093");

        var picked = context.GetRandomNodeForService(ServiceType.Query);

        Assert.Same(node, picked);
    }

    private static ClusterContext CreateContext()
    {
        return new ClusterContext(null, new ClusterOptions().WithPasswordAuthentication("u", "p"));
    }

    private static IClusterNode AddQueryNode(ClusterContext context, string uri)
        => AddQueryNode(context, new Uri(uri));

    private static IClusterNode AddQueryNode(ClusterContext context, Uri uri)
    {
        var node = new Mock<IClusterNode>();
        node.SetupGet(x => x.HasQuery).Returns(true);
        node.SetupGet(x => x.QueryUri).Returns(uri);
        context.AddNode(node.Object);
        return node.Object;
    }

    private static IClusterNode AddSearchNode(ClusterContext context, Uri uri)
    {
        var node = new Mock<IClusterNode>();
        node.SetupGet(x => x.HasSearch).Returns(true);
        node.SetupGet(x => x.SearchUri).Returns(uri);
        context.AddNode(node.Object);
        return node.Object;
    }
}
