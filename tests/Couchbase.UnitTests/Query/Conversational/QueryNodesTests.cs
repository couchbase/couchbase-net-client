using System;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Query.Conversational;
using Moq;
using Xunit;

#nullable enable

namespace Couchbase.UnitTests.Query.Conversational
{
    public class QueryNodesTests
    {
        [Fact]
        public void TryResolve_KnownUuid_ReturnsQueryUri()
        {
            var nodes = new[]
            {
                Node("uuid-a", "http://a:8093/query/service"),
                Node("uuid-b", "http://b:8093/query/service")
            };

            Assert.True(QueryNodes.TryResolve(nodes, "uuid-b", out var queryUri));
            Assert.Equal(new Uri("http://b:8093/query/service"), queryUri);
        }

        [Theory]
        [InlineData("uuid-c")]
        [InlineData("UUID-A")]
        public void TryResolve_UnknownUuid_ReturnsFalse(string nodeUuid)
        {
            var nodes = new[]
            {
                Node("uuid-a", "http://a:8093/query/service"),
                Node("uuid-b", "http://b:8093/query/service")
            };

            Assert.False(QueryNodes.TryResolve(nodes, nodeUuid, out var queryUri));
            Assert.Null(queryUri);
        }

        [Fact]
        public void TryResolve_NodeWithoutQuery_ReturnsFalse()
        {
            var nodes = new[] { Node("uuid-a", "http://a/", hasQuery: false) };

            Assert.False(QueryNodes.TryResolve(nodes, "uuid-a", out _));
        }

        [Fact]
        public void TryResolve_QueryNodeWithoutUri_ReturnsFalse()
        {
            var nodes = new[] { Node("uuid-a", null) };

            Assert.False(QueryNodes.TryResolve(nodes, "uuid-a", out _));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TryResolve_EmptyUuid_ReturnsFalse(string? nodeUuid)
        {
            var nodes = new[] { Node("", "http://a:8093/query/service") };

            Assert.False(QueryNodes.TryResolve(nodes, nodeUuid, out _));
        }

        [Fact]
        public void TryResolve_SeveralNodeObjectsForOneUuid_ReturnsQueryUri()
        {
            // The node list holds a global node plus one node per open bucket.
            var nodes = new[]
            {
                Node("uuid-a", null),
                Node("uuid-a", "http://a:8093/query/service"),
                Node("uuid-a", "http://a:8093/query/service")
            };

            Assert.True(QueryNodes.TryResolve(nodes, "uuid-a", out var queryUri));
            Assert.Equal(new Uri("http://a:8093/query/service"), queryUri);
        }

        [Fact]
        public void TrySelectRandom_SkipsIneligibleNodes()
        {
            var nodes = new[]
            {
                Node("uuid-kv", "http://kv/", hasQuery: false),
                Node("", "http://no-uuid:8093/query/service"),
                Node("uuid-a", "http://a:8093/query/service"),
                Node("uuid-a", "http://a:8093/query/service")
            };

            for (var i = 0; i < 20; i++)
            {
                Assert.True(QueryNodes.TrySelectRandom(nodes, out var nodeUuid, out var queryUri));
                Assert.Equal("uuid-a", nodeUuid);
                Assert.Equal(new Uri("http://a:8093/query/service"), queryUri);
            }
        }

        [Fact]
        public void TrySelectRandom_ReturnsUuidAndUriOfOneNode()
        {
            var nodes = new[]
            {
                Node("uuid-a", "http://a:8093/query/service"),
                Node("uuid-b", "http://b:8093/query/service")
            };

            for (var i = 0; i < 20; i++)
            {
                Assert.True(QueryNodes.TrySelectRandom(nodes, out var nodeUuid, out var queryUri));
                var expected = nodeUuid == "uuid-a" ? "http://a:8093/query/service" : "http://b:8093/query/service";
                Assert.Contains(nodeUuid, new[] { "uuid-a", "uuid-b" });
                Assert.Equal(new Uri(expected), queryUri);
            }
        }

        [Fact]
        public void TrySelectRandom_NoEligibleNode_ReturnsFalse()
        {
            var nodes = new[]
            {
                Node("uuid-kv", "http://kv/", hasQuery: false),
                Node("", "http://no-uuid:8093/query/service"),
                Node("uuid-no-uri", null)
            };

            Assert.False(QueryNodes.TrySelectRandom(nodes, out var nodeUuid, out var queryUri));
            Assert.Null(nodeUuid);
            Assert.Null(queryUri);
        }

        [Fact]
        public void TrySelectRandom_NoNodes_ReturnsFalse()
        {
            Assert.False(QueryNodes.TrySelectRandom(Array.Empty<IClusterNode>(), out _, out _));
        }

        [Fact]
        public void HasSharedQueryEndpoint_TwoUuidsOnOneHostAndPort_ReturnsTrue()
        {
            var nodes = new[]
            {
                Node("uuid-a", "https://lb.example.com:18093/query/service"),
                Node("uuid-b", "https://lb.example.com:18093/query/service")
            };

            Assert.True(QueryNodes.HasSharedQueryEndpoint(nodes, enableTls: false));
        }

        [Fact]
        public void HasSharedQueryEndpoint_HostDiffersOnlyInCase_ReturnsTrue()
        {
            var nodes = new[]
            {
                Node("uuid-a", "https://LB.example.com:18093/query/service"),
                Node("uuid-b", "https://lb.EXAMPLE.com:18093/query/service")
            };

            Assert.True(QueryNodes.HasSharedQueryEndpoint(nodes, enableTls: false));
        }

        [Fact]
        public void HasSharedQueryEndpoint_DistinctPorts_ReturnsFalse()
        {
            var nodes = new[]
            {
                Node("uuid-a", "http://127.0.0.1:9499/query/service"),
                Node("uuid-b", "http://127.0.0.1:9500/query/service")
            };

            Assert.False(QueryNodes.HasSharedQueryEndpoint(nodes, enableTls: false));
        }

        [Fact]
        public void HasSharedQueryEndpoint_DistinctHosts_ReturnsFalse()
        {
            var nodes = new[]
            {
                Node("uuid-a", "http://a:8093/query/service"),
                Node("uuid-b", "http://b:8093/query/service")
            };

            Assert.False(QueryNodes.HasSharedQueryEndpoint(nodes, enableTls: false));
        }

        [Fact]
        public void HasSharedQueryEndpoint_OneUuidListedTwice_ReturnsFalse()
        {
            var nodes = new[]
            {
                Node("uuid-a", "https://lb.example.com:18093/query/service"),
                Node("uuid-a", "https://lb.example.com:18093/query/service")
            };

            Assert.False(QueryNodes.HasSharedQueryEndpoint(nodes, enableTls: false));
        }

        [Fact]
        public void HasSharedQueryEndpoint_SingleNode_ReturnsFalse()
        {
            var nodes = new[] { Node("uuid-a", "https://lb.example.com:18093/query/service") };

            Assert.False(QueryNodes.HasSharedQueryEndpoint(nodes, enableTls: false));
        }

        [Fact]
        public void HasSharedQueryEndpoint_IgnoresNodesWithoutQueryOrUuid()
        {
            var nodes = new[]
            {
                Node("uuid-a", "https://lb.example.com:18093/query/service"),
                Node("uuid-kv1", "https://lb.example.com/", hasQuery: false),
                Node("uuid-kv2", "https://lb.example.com/", hasQuery: false),
                Node("", "https://lb.example.com:18093/query/service")
            };

            Assert.False(QueryNodes.HasSharedQueryEndpoint(nodes, enableTls: false));
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public void HasSharedQueryEndpoint_ComparesThePortForTheTlsSetting(bool enableTls, bool expected)
        {
            var nodes = new[]
            {
                Node("uuid-a", "https://lb.example.com:18093/query/service", n1QlPort: 8093),
                Node("uuid-b", "https://lb.example.com:18093/query/service", n1QlPort: 8094)
            };

            Assert.Equal(expected, QueryNodes.HasSharedQueryEndpoint(nodes, enableTls));
        }

        private static IClusterNode Node(string nodeUuid, string? queryUri, bool hasQuery = true, int? n1QlPort = null)
        {
            var uri = queryUri is null ? null : new Uri(queryUri);
            var node = new Mock<IClusterNode>();
            node.SetupGet(x => x.NodeUuid).Returns(nodeUuid);
            node.SetupGet(x => x.HasQuery).Returns(hasQuery);
            node.SetupGet(x => x.QueryUri).Returns(uri!);
            node.SetupGet(x => x.NodesAdapter).Returns(uri is null
                ? null!
                : new NodeAdapter { Hostname = uri.Host, N1Ql = n1QlPort ?? uri.Port, N1QlSsl = uri.Port });
            return node.Object;
        }
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
