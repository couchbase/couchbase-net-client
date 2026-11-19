using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Couchbase.Core;
using Couchbase.Core.CircuitBreakers;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.UnitTests.Core.Diagnostics.Tracing.Fakes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core
{
    public class BucketNodeListTests
    {
        #region Add

        [Fact]
        public void Add_NotInCollection_ReturnsTrue()
        {
            // Arrange

            var node = CreateMockNode("default", CreateEndpoint(1)).Object;

            var nodes = new BucketNodeList();

            // Act

            var result = nodes.Add(node);

            // Assert

            Assert.True(result);
        }

        [Fact]
        public void Add_NotInCollection_RegistersForEvents()
        {
            // Arrange

            var node = CreateMockNode("default",CreateEndpoint(1));

            var nodes = new BucketNodeList();

            // Act

            nodes.Add(node.Object);

            // Assert

            node.VerifyAdd(m => m.KeyEndPointsChanged += It.IsAny<NotifyCollectionChangedEventHandler>(), Times.Once);
        }

        [Fact]
        public void Add_InCollection_ReturnsFalse()
        {
            // Arrange

            var node = CreateMockNode("default",CreateEndpoint(1)).Object;

            var nodes = new BucketNodeList
            {
                node
            };

            // Act

            var result = nodes.Add(node);

            // Assert

            Assert.False(result);
            Assert.Equal(1, nodes.Count);
        }

        [Fact]
        public void Add_NotInCollection_RegistersAllEndpoints()
        {
            // Arrange

            var node = CreateMockNode("default",CreateEndpoint(1), CreateEndpoint(2)).Object;

            var nodes = new BucketNodeList();

            // Act

            nodes.Add(node);

            // Assert

            Assert.True(nodes.TryGet(CreateEndpoint(1), out var resultNode));
            Assert.Equal(node, resultNode);

            Assert.True(nodes.TryGet(CreateEndpoint(2), out resultNode));
            Assert.Equal(node, resultNode);
        }

        #endregion

        #region Remove

        [Fact]
        public void Remove_NotInCollection_ReturnsFalse()
        {
            // Arrange

            var nodes = new BucketNodeList();

            // Act

            var result = nodes.Remove(CreateEndpoint(1), "default",  out _);

            // Assert

            Assert.False(result);
        }

        [Fact]
        public void Remove_InCollection_ReturnsTrue()
        {
            // Arrange

            var node = CreateMockNode("default",CreateEndpoint(1)).Object;

            var nodes = new BucketNodeList
            {
                node
            };

            // Act

            var result = nodes.Remove(CreateEndpoint(1), "default", out var removedNode);

            // Assert

            Assert.True(result);
            Assert.Equal(node, removedNode);
        }

        [Fact]
        public void Remove_InCollection_UnregistersForEvents()
        {
            // Arrange

            var node = CreateMockNode("default",CreateEndpoint(1));

            var nodes = new BucketNodeList
            {
                node.Object
            };

            // Act

            nodes.Remove(CreateEndpoint(1), "default", out _);

            // Assert

            node.VerifyRemove(m => m.KeyEndPointsChanged -= It.IsAny<NotifyCollectionChangedEventHandler>(), Times.AtLeastOnce);
        }

        [Fact]
        public void Remove_InCollection_ReducesCount()
        {
            // Arrange

            var node = CreateMockNode("default",CreateEndpoint(1)).Object;

            var nodes = new BucketNodeList
            {
                node
            };
            Assert.Equal(1, nodes.Count);

            // Act

            var result = nodes.Remove(CreateEndpoint(1), "default", out _);

            // Assert

            Assert.Equal(0, nodes.Count);
        }

        [Fact]
        public void Remove_AnyMatching_InCollection_ReducesCount()
        {
            // Arrange

            var node = CreateMockNode("default",CreateEndpoint(1)).Object;
            var node1 = CreateMockNode("default",CreateEndpoint(1)).Object;

            var nodes = new BucketNodeList
            {
                node,
                node1
            };
            Assert.Equal(2, nodes.Count);

            // Act

            var result = nodes.Remove(CreateEndpoint(1), "default", out _);

            // Assert

            Assert.Equal(0, nodes.Count);
        }

        [Fact]
        public void Remove_InCollection_UnregistersAllEndpoints()
        {
            // Arrange

            var node = CreateMockNode("default",CreateEndpoint(1), CreateEndpoint(2)).Object;

            var nodes = new BucketNodeList()
            {
                node
            };

            // Act

            nodes.Remove(CreateEndpoint(1), "default", out _);

            // Assert

            Assert.False(nodes.TryGet(CreateEndpoint(1), out _));
            Assert.False(nodes.TryGet(CreateEndpoint(2), out _));
        }

        #endregion

        #region Clear

        [Fact]
        public void Clear_RemovesNodes()
        {
            // Arrange

            var node1 = CreateMockNode("default", CreateEndpoint(1)).Object;
            var node2 = CreateMockNode("default", CreateEndpoint(2)).Object;

            var nodes = new BucketNodeList
            {
                node1,
                node2
            };
            Assert.Equal(2, nodes.Count);

            // Act

            var result = nodes.Clear();

            // Assert

            Assert.Equal(0, nodes.Count);
        }

        [Fact]
        public void Clear_ReturnsRemovedNodes()
        {
            // Arrange

            var node1 = CreateMockNode("default", CreateEndpoint(1)).Object;
            var node2 = CreateMockNode("default", CreateEndpoint(2)).Object;

            var nodes = new BucketNodeList
            {
                node1,
                node2
            };
            Assert.Equal(2, nodes.Count);

            // Act

            var result = nodes.Clear();

            // Assert

            Assert.Equal(2, result.Count);
            Assert.Contains(node1, result);
            Assert.Contains(node2, result);
        }

        [Fact]
        public void Clear_RemovesFromLookup()
        {
            // Arrange

            var node1 = CreateMockNode("default", CreateEndpoint(1)).Object;
            var node2 = CreateMockNode("default", CreateEndpoint(2)).Object;

            var nodes = new BucketNodeList
            {
                node1,
                node2
            };
            Assert.Equal(2, nodes.Count);

            // Act

            nodes.Clear();

            // Assert

            Assert.False(nodes.TryGet(CreateEndpoint(1), out _));
            Assert.False(nodes.TryGet(CreateEndpoint(2), out _));
        }

        [Fact]
        public void Clear_UnregistersEvents()
        {
            // Arrange

            var node1 = CreateMockNode("default", CreateEndpoint(1));
            var node2 = CreateMockNode("default", CreateEndpoint(2));

            var nodes = new BucketNodeList
            {
                node1.Object,
                node2.Object
            };
            Assert.Equal(2, nodes.Count);

            // Act

            nodes.Clear();

            // Assert

            node1.VerifyRemove(m => m.KeyEndPointsChanged -= It.IsAny<NotifyCollectionChangedEventHandler>(), Times.AtLeastOnce);
            node2.VerifyRemove(m => m.KeyEndPointsChanged -= It.IsAny<NotifyCollectionChangedEventHandler>(), Times.AtLeastOnce);
        }

        #endregion

        #region ReplaceAll

        [Fact]
        public void ReplaceAll_SharedNode_StaysReachableWhileNodesChange()
        {
            var shared = CreateMockNode("default", CreateEndpoint(1));
            var leaving = CreateMockNode("default", CreateEndpoint(2));
            var joining = CreateMockNode("default", CreateEndpoint(3));
            var nodes = new BucketNodeList { shared.Object, leaving.Object };

            // ReplaceAll reads KeyEndPoints of each node it adds or removes, so each read checks the shared node.
            var reachable = new List<bool>();
            IReadOnlyCollection<HostEndpointWithPort> Observe(Mock<IClusterNode> node)
            {
                reachable.Add(nodes.TryGet(CreateEndpoint(1), out var found) && ReferenceEquals(found, shared.Object));
                return new ReadOnlyCollection<HostEndpointWithPort>(new[] { node.Object.EndPoint });
            }
            leaving.Setup(p => p.KeyEndPoints).Returns(() => Observe(leaving));
            joining.Setup(p => p.KeyEndPoints).Returns(() => Observe(joining));

            nodes.ReplaceAll(new[] { shared.Object, joining.Object });

            Assert.Equal(new[] { true, true }, reachable);
            Assert.True(nodes.TryGet(CreateEndpoint(1), out var sharedNode));
            Assert.Same(shared.Object, sharedNode);
            Assert.True(nodes.TryGet(CreateEndpoint(3), out var joinedNode));
            Assert.Same(joining.Object, joinedNode);
            Assert.False(nodes.TryGet(CreateEndpoint(2), out _));
            Assert.Equal(2, nodes.Count);
            shared.VerifyAdd(m => m.KeyEndPointsChanged += It.IsAny<NotifyCollectionChangedEventHandler>(), Times.Once);
            shared.VerifyRemove(m => m.KeyEndPointsChanged -= It.IsAny<NotifyCollectionChangedEventHandler>(), Times.Never);
        }

        // Two nodes of the same bucket at the same endpoint are equal, so the old entry must be found by reference.
        [Fact]
        public void ReplaceAll_EndpointMovesToANewNode_EndsOnTheNewNode()
        {
            var bucket = new FakeBucket("default", new ClusterOptions());
            var oldNode = CreateClusterNode(CreateEndpoint(1), bucket);
            var newNode = CreateClusterNode(CreateEndpoint(1), bucket);
            Assert.Equal(oldNode, newNode);
            var nodes = new BucketNodeList { oldNode };

            nodes.ReplaceAll(new IClusterNode[] { newNode });

            Assert.True(nodes.TryGet(CreateEndpoint(1), out var found));
            Assert.Same(newNode, found);
            Assert.Same(newNode, Assert.Single(nodes));
            Assert.Same(newNode, Assert.Single(nodes.LookupDictionary).Value);
        }

        [Fact]
        public void ReplaceAll_RemovedNode_IsUnsubscribedAndUnmapped()
        {
            var staying = CreateMockNode("default", CreateEndpoint(1));
            var removed = CreateMockNode("default", CreateEndpoint(2), CreateEndpoint(3));
            var nodes = new BucketNodeList { staying.Object, removed.Object };

            nodes.ReplaceAll(new[] { staying.Object });

            removed.VerifyRemove(m => m.KeyEndPointsChanged -= It.IsAny<NotifyCollectionChangedEventHandler>(), Times.Once);
            removed.Verify(m => m.Dispose(), Times.Never);
            Assert.False(nodes.TryGet(CreateEndpoint(2), out _));
            Assert.False(nodes.TryGet(CreateEndpoint(3), out _));
            Assert.Same(staying.Object, Assert.Single(nodes));
            Assert.Single(nodes.LookupDictionary);
        }

        [Fact]
        public void ReplaceAll_EqualSets_ChangesNothing()
        {
            var node1 = CreateMockNode("default", CreateEndpoint(1));
            var node2 = CreateMockNode("default", CreateEndpoint(2));
            var nodes = new BucketNodeList { node1.Object, node2.Object };
            var lookup = new Dictionary<HostEndpointWithPort, IClusterNode>(nodes.LookupDictionary);

            nodes.ReplaceAll(new[] { node2.Object, node1.Object });

            Assert.Equal(lookup.Count, nodes.LookupDictionary.Count);
            foreach (var entry in lookup)
            {
                Assert.Same(entry.Value, nodes.LookupDictionary[entry.Key]);
            }
            Assert.Equal(2, nodes.Count);
            foreach (var node in new[] { node1, node2 })
            {
                node.VerifyAdd(m => m.KeyEndPointsChanged += It.IsAny<NotifyCollectionChangedEventHandler>(), Times.Once);
                node.VerifyRemove(m => m.KeyEndPointsChanged -= It.IsAny<NotifyCollectionChangedEventHandler>(), Times.Never);
            }
        }

        #endregion

        #region GetEnumerator

        [Fact]
        public void GetEnumerator_ChangeWhileEnumerating_GetsOriginalList()
        {
            // Arrange

            var node1 = CreateMockNode("default",CreateEndpoint(1)).Object;
            var node2 = CreateMockNode("default",CreateEndpoint(2)).Object;

            var nodes = new BucketNodeList
            {
                node1,
                node2
            };
            Assert.Equal(2, nodes.Count);

            // Act

            using var enumerator = nodes.GetEnumerator();
            Assert.True(enumerator.MoveNext());

            Assert.True(nodes.Remove(CreateEndpoint(1), "default", out _));

            // Assert

            Assert.True(enumerator.MoveNext());
            Assert.False(enumerator.MoveNext());
        }

        #endregion

        #region KeyEndPoint Monitoring

        [Fact]
        public void AddKeyEndPoint_RemovesFromLookup()
        {
            // Arrange

            var node = CreateMockNode("default", CreateEndpoint(1));

            var nodes = new BucketNodeList
            {
                node.Object
            };

            var args = new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Add,
                new List<HostEndpointWithPort> { CreateEndpoint(2) },
                1);

            // Act

            node.Raise(m => m.KeyEndPointsChanged += null, args);

            // Assert

            Assert.True(nodes.TryGet(CreateEndpoint(2), out _));
        }

        [Fact]
        public void RemoveKeyEndPoint_RemovesFromLookup()
        {
            // Arrange

            var node = CreateMockNode("default", CreateEndpoint(1), CreateEndpoint(2));

            var nodes = new BucketNodeList
            {
                node.Object
            };

            var args = new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Remove,
                new List<HostEndpointWithPort> { CreateEndpoint(2) },
                1);

            // Act

            node.Raise(m => m.KeyEndPointsChanged += null, args);

            // Assert

            Assert.False(nodes.TryGet(CreateEndpoint(2), out _));
        }

        #endregion

        #region Helpers

        [Fact]
        public void Remove_Endpoint_With_Multiple_Buckets()
        {
            var node1 = CreateMockNode("default",CreateEndpoint(1));
            var node2 = CreateMockNode("default",CreateEndpoint(2));
            var node3 = CreateMockNode("default2",CreateEndpoint(3));
            var node4 = CreateMockNode("default2",CreateEndpoint(4));

            var nodes = new BucketNodeList { node1.Object, node2.Object, node3.Object, node4.Object };
            var result = nodes.Remove(node3.Object.EndPoint, "default2", out IClusterNode oldNode);
            Assert.True(result);
            Assert.Equal(3, nodes.Count);
            Assert.Equal(3, nodes.LookupDictionary.Count);

            result = nodes.Remove(node4.Object.EndPoint, "default2", out IClusterNode oldNode2);
            Assert.True(result);
            Assert.Equal(2, nodes.Count);
            Assert.Equal(2, nodes.LookupDictionary.Count);
        }

        private HostEndpointWithPort CreateEndpoint(byte i)
        {
            return new HostEndpointWithPort($"127.0.0.{i}", 11210);
        }

        private static ClusterNode CreateClusterNode(HostEndpointWithPort endPoint, IBucket owner)
        {
            var options = new ClusterOptions().WithPasswordAuthentication("username", "password");
            return new ClusterNode(new ClusterContext(null, options), new Mock<IConnectionPoolFactory>().Object,
                new Mock<ILogger<ClusterNode>>().Object,
                new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy()),
                new Mock<ICircuitBreaker>().Object,
                new Mock<ISaslMechanismFactory>().Object,
                new Redactor(RedactionLevel.None),
                endPoint,
                new NodeAdapter { Hostname = endPoint.Host, KeyValue = endPoint.Port },
                NoopRequestTracer.Instance,
                new Mock<IOperationConfigurator>().Object)
            {
                Owner = owner
            };
        }

        private Mock<IClusterNode> CreateMockNode( string bucketName, params HostEndpointWithPort[] endPoints)
        {
            var node = new Mock<IClusterNode>();
            node
                .Setup(p => p.EndPoint)
                .Returns(endPoints[0]);
            node
                .Setup(p => p.KeyEndPoints)
                .Returns(new ReadOnlyCollection<HostEndpointWithPort>(endPoints));
            node
                .SetupAdd(m => m.KeyEndPointsChanged += It.IsAny<NotifyCollectionChangedEventHandler>());
            node
                .SetupRemove(m => m.KeyEndPointsChanged -= It.IsAny<NotifyCollectionChangedEventHandler>());
            node
                .Setup(x => x.Owner)
                .Returns(new FakeBucket(bucketName, new ClusterOptions()));

            return node;
        }

        #endregion
    }
}
