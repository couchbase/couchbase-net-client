using System;
using Couchbase.Core;
using Moq;
using Xunit;

#nullable enable

namespace Couchbase.UnitTests.Core;

public class ServiceUriProviderTests
{
    [Fact]
    public void GetQueryNode_KnownUuid_ReturnsThatNode()
    {
        var context = CreateContext();
        AddNode(context, "uuid-a", "http://a:8093/query/service");
        var nodeB = AddNode(context, "uuid-b", "http://b:8093/query/service");

        var provider = new ServiceUriProvider(context);

        for (var i = 0; i < 20; i++)
        {
            Assert.Same(nodeB, provider.GetQueryNode("uuid-b"));
        }
    }

    [Fact]
    public void GetQueryNode_UnknownUuid_ReturnsNull()
    {
        var context = CreateContext();
        AddNode(context, "uuid-a", "http://a:8093/query/service");

        Assert.Null(new ServiceUriProvider(context).GetQueryNode("uuid-gone"));
    }

    [Fact]
    public void GetQueryNode_NodeWithoutQuery_ReturnsNull()
    {
        var context = CreateContext();
        AddNode(context, "uuid-a", "http://a:8091/", hasQuery: false);

        Assert.Null(new ServiceUriProvider(context).GetQueryNode("uuid-a"));
    }

    private static ClusterContext CreateContext() =>
        new(null, new ClusterOptions().WithPasswordAuthentication("u", "p"));

    private static IClusterNode AddNode(ClusterContext context, string nodeUuid, string queryUri, bool hasQuery = true)
    {
        var node = new Mock<IClusterNode>();
        node.SetupGet(x => x.NodeUuid).Returns(nodeUuid);
        node.SetupGet(x => x.HasQuery).Returns(hasQuery);
        node.SetupGet(x => x.QueryUri).Returns(new Uri(queryUri));
        context.AddNode(node.Object);
        return node.Object;
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2026 Couchbase, Inc.
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
