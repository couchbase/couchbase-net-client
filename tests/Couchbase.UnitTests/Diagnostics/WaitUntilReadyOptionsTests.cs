using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Errors;
using Couchbase.Diagnostics;
using Couchbase.Management.Buckets;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Diagnostics
{
    public class WaitUntilReadyOptionsTests
    {
        [Fact]
        public void Test_ServiceTypes_Defaults_To_All_Services()
        {
            //arrange/act

            var options = new WaitUntilReadyOptions();
            var serviceTypes = options.EffectiveServiceTypes(null).ToList();
            //assert

            Assert.Equal(5, serviceTypes.Count);
            Assert.Contains(serviceTypes, type => type == ServiceType.KeyValue );
            Assert.Contains(serviceTypes, type => type == ServiceType.Query);
            Assert.Contains(serviceTypes, type => type == ServiceType.Search);
            Assert.Contains(serviceTypes, type => type == ServiceType.Analytics);
            Assert.Contains(serviceTypes, type => type == ServiceType.KeyValue);
        }

        [Fact]
        public void Can_Override_ServiceType_Defaults()
        {
            //arrange

            var options = new WaitUntilReadyOptions();

            //act

            options.ServiceTypes(ServiceType.KeyValue, ServiceType.Query);
            var serviceTypes = options.EffectiveServiceTypes(null).ToList();

            //assert

            Assert.Equal(2, serviceTypes.Count);
            Assert.Contains(serviceTypes, type => type == ServiceType.KeyValue);
            Assert.Contains(serviceTypes, type => type == ServiceType.Query);
        }

        [Fact]
        public void Empty_ServiceTypes_UsesDefaults()
        {
            //arrange

            var options = new WaitUntilReadyOptions();

            //act

            options.ServiceTypes();
            var serviceTypes = options.EffectiveServiceTypes(null).ToList();

            //assert

            Assert.NotEmpty(serviceTypes);
            Assert.Contains(serviceTypes, type => type == ServiceType.KeyValue);
        }

        [Fact]
        public void EffectiveServiceTypes_EmptyClusterInfo_Checks_At_Least_Kv()
        {
            //arrange

            var options = new WaitUntilReadyOptions();
            var clusterContext = new ClusterContext()
            {
                GlobalConfig = new BucketConfig()
            };

            //act
            var serviceTypes = options.EffectiveServiceTypes(clusterContext).ToList();

            //assert

            Assert.NotEmpty(serviceTypes);
            Assert.Contains(serviceTypes, type => type == ServiceType.KeyValue);
        }

        [Fact]
        public void EffectiveServiceTypes_Uses_Services_From_Cluster()
        {
            //arrange

            var options = new WaitUntilReadyOptions();
            var clusterContext = new ClusterContext();
            clusterContext.AddNode(new MockClusterNode() { HasKv = true });
            clusterContext.AddNode(new MockClusterNode() { HasKv = true, HasQuery = true });
            clusterContext.AddNode(new MockClusterNode() { HasAnalytics = true });

            //act
            var serviceTypes = options.EffectiveServiceTypes(clusterContext).ToList();

            //assert

            Assert.NotEmpty(serviceTypes);
            Assert.Contains(serviceTypes, type => type == ServiceType.KeyValue);
            Assert.Contains(serviceTypes, type => type == ServiceType.Query);
            Assert.Contains(serviceTypes, type => type == ServiceType.Analytics);
            Assert.DoesNotContain(serviceTypes, type => type == ServiceType.Views);
        }

        private class MockClusterNode : IClusterNode
        {
            public bool HasViews { get; internal set; } = false;
            public bool HasAnalytics { get; internal set; } = false;
            public bool HasQuery { get; internal set; } = false;
            public bool HasSearch { get; internal set; } = false;
            public bool HasKv { get; internal set; } = false;

            #region unused by these tests
            public bool HasEventing { get; internal set; } = false;
            public bool HasManagement { get; internal set; } = false;

            public void Dispose()
            {
                throw new NotImplementedException();
            }

            public IBucket Owner { get; set; }
            public NodeAdapter NodesAdapter { get; set; }
            public HostEndpointWithPort EndPoint { get; }
            public BucketType BucketType { get; }
            public string BucketName { get; }
            public IReadOnlyCollection<HostEndpointWithPort> KeyEndPoints { get; }
            public Uri EventingUri { get; set; }
            public Uri QueryUri { get; set; }
            public Uri AnalyticsUri { get; set; }
            public Uri SearchUri { get; set; }
            public Uri ViewsUri { get; set; }
            public Uri ManagementUri { get; set; }
            public ErrorMap ErrorMap { get; set; }
            public ServerFeatureSet ServerFeatures { get; }
            public IConnectionPool ConnectionPool { get; }
            public List<Exception> Exceptions { get; set; }
            public Task HelloHello()
            {
                throw new NotImplementedException();
            }

            public bool IsAssigned { get; }

            public DateTime? LastViewActivity { get; }
            public DateTime? LastQueryActivity { get; }
            public DateTime? LastSearchActivity { get; }
            public DateTime? LastAnalyticsActivity { get; }
            public DateTime? LastKvActivity { get; }
            public DateTime? LastEventingActivity { get; }
            public Task<Manifest> GetManifest()
            {
                throw new NotImplementedException();
            }

            public Task SelectBucketAsync(string bucketName, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<BucketConfig> GetClusterMap(ConfigVersion? configVersion = default, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<ResponseStatus> ExecuteOp(IOperation op, CancellationTokenPair tokenPair = default)
            {
                throw new NotImplementedException();
            }

            public Task<ResponseStatus> ExecuteOp(IConnection connection, IOperation op, CancellationTokenPair tokenPair = default)
            {
                throw new NotImplementedException();
            }

            public Task<ResponseStatus> SendAsync(IOperation op, CancellationTokenPair tokenPair = default)
            {
                throw new NotImplementedException();
            }

            public event NotifyCollectionChangedEventHandler KeyEndPointsChanged;
            #endregion
        }
    }
}
