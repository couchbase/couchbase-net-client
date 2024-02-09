#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Retry;
using Couchbase.Management.Buckets;
using Couchbase.Management.Query;
using Couchbase.Management.Search;
using Couchbase.Protostellar.Query.V1;
using Couchbase.Search.Queries.Simple;
using Couchbase.Stellar;
using Couchbase.Stellar.Core;
using Couchbase.Stellar.Search;
using Couchbase.Stellar.Util;
using Grpc.Core;
using Grpc.Net.Client;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Stellar;

public class ClusterTests
{
      #region Stellar

      [Fact]
      public async Task Throw_UnsupportedInProtostellarException_Diagnostics()
      {
          var cluster = await CreateCluster();
          await Assert.ThrowsAsync<UnsupportedInProtostellarException>(async () => await cluster.DiagnosticsAsync());
      }

      [Fact]
      public async Task Throw_UnsupportedInProtostellarException_Ping()
      {
          var cluster = await CreateCluster();
          await Assert.ThrowsAsync<UnsupportedInProtostellarException>(async () => await cluster.PingAsync());
      }

      [Fact]
      public async Task Throw_UnsupportedInProtostellarException_Users()
      {
          var cluster = await CreateCluster();
          Assert.Throws<UnsupportedInProtostellarException>(() => cluster.Users);
      }

      [Fact]
      public async Task Throw_UnsupportedInProtostellarException_EventingFunctions()
      {
          var cluster = await CreateCluster();
          Assert.Throws<UnsupportedInProtostellarException>(() => cluster.EventingFunctions);
      }


      [Fact]
      public async Task Throw_UnsupportedInProtostellarException_WaitUntilReadyAsync()
      {
          var cluster = await CreateCluster();
          await Assert.ThrowsAsync<UnsupportedInProtostellarException>(async () => await cluster.WaitUntilReadyAsync(TimeSpan.Zero));
      }

      [Fact]
      public async Task Throw_UnsupportedInProtostellarException_AnalyticsIndexes()
      {
          var cluster = await CreateCluster();
          Assert.Throws<UnsupportedInProtostellarException>(() => cluster.AnalyticsIndexes);
      }

      [Fact]
      public async Task Throw_UnsupportedInProtostellarException_ClusterServices()
      {
          var cluster = await CreateCluster();
          Assert.Throws<UnsupportedInProtostellarException>(() => cluster.ClusterServices);
      }

      [Fact]
      public async Task Throw_AggregateException_On_Cluster_QueryAsync_If_ConnectAsync_Fails()
      {
          var cluster = await CreateCluster();
          await Assert.ThrowsAsync<AggregateException>(async () => await cluster.QueryAsync<dynamic>("SELECT d.* from default2 as d;"));
      }

      [Fact]
      public async Task Throw_AggregateException_On_Cluster_AnalyticsQueryAsync_If_ConnectAsync_Fails()
      {
          var cluster = await CreateCluster();
          await Assert.ThrowsAsync<AggregateException>(async () => await cluster.AnalyticsQueryAsync<dynamic>("SELECT 1;"));
      }

      [Theory]
      [InlineData("couchbase://xxx", typeof(Cluster))]
      [InlineData("couchbases://xxx", typeof(Cluster))]
#if NETCOREAPP3_1_OR_GREATER
      [InlineData("couchbase2://xxx", typeof(Couchbase.Stellar.StellarCluster))]
#endif
      public async Task Test_Schema_Delivers_The_Correct_ICluster_Impl(string connectionString, Type type)
      {
          var options = new ClusterOptions().WithCredentials("Administrator", "password");
          options.KvConnectTimeout = TimeSpan.FromMilliseconds(1);
          var cluster = await Cluster.ConnectAsync(connectionString,options);

          Assert.IsType(type, cluster);
      }

      public async Task<ICluster> CreateCluster()
      {
          var connectionString = "couchbase2://xxx";

          var options = new ClusterOptions().WithCredentials("Administrator", "password");
          options.KvConnectTimeout = TimeSpan.FromMilliseconds(1);
          options.HttpIgnoreRemoteCertificateMismatch = true;
          options.KvIgnoreRemoteCertificateNameMismatch = true;

          return await Cluster.ConnectAsync(connectionString,options);
      }
      #endregion

      [Fact]
      public async Task Buckets_Are_Cached()
      {
          var cluster = CreateClusterFromMocks();
          var bucket1 = await cluster.BucketAsync("default");
          var bucket2 = await cluster.BucketAsync("default");

          Assert.Equal(bucket1.GetHashCode(), bucket2.GetHashCode());
      }

      [Fact]
      public async Task Disposed_Buckets_Are_Removed_From_Cache()
      {
          var cluster = CreateClusterFromMocks();
          var bucket1 = await cluster.BucketAsync("default");

          bucket1.Dispose();

          var clusterExt = (IClusterExtended)cluster;
          Assert.False(clusterExt.BucketExists("default"));
      }

      [Fact]
      public async Task Dispose_Removes_All_Buckets()
      {
          var cluster = CreateClusterFromMocks();
          var bucket1 = await cluster.BucketAsync("default");
          var bucket2 = await cluster.BucketAsync("default1");

          cluster.Dispose();

          var clusterExt = (IClusterExtended)cluster;
          Assert.False(clusterExt.BucketExists("default"));
          Assert.False(clusterExt.BucketExists("default1"));
      }

      [Fact]
      public void Cluster_Can_Be_Mocked()
      {
          var cluster = CreateClusterFromMocks();

          Assert.NotNull(cluster);
      }

      [Fact]
      public void Dispose_Is_Idempotent()
      {
          var cluster = CreateClusterFromMocks();

          cluster.Dispose();
          cluster.Dispose();//no side effect
      }

      [Fact]
      public async Task Throw_ODE_When_BucketAsync_Called_After_Being_Disposed()
      {
          var cluster = CreateClusterFromMocks();

          cluster.Dispose();

         await Assert.ThrowsAsync<ObjectDisposedException>(async ()=> await cluster.BucketAsync("default"));
      }

      [Fact]
      public async Task Throw_ODE_When_QueryAsync_Called_After_Being_Disposed()
      {
          var cluster = CreateClusterFromMocks();

          cluster.Dispose();

          await Assert.ThrowsAsync<ObjectDisposedException>(async ()=> await cluster.QueryAsync<dynamic>("SELECT 1;"));
      }

      [Fact]
      public async Task Throw_ODE_When_SearchQueryAsync_Called_After_Being_Disposed()
      {
          var cluster = CreateClusterFromMocks();
          cluster.Dispose();

          await Assert.ThrowsAsync<ObjectDisposedException>(async ()=> await cluster.SearchQueryAsync("indexname", new TermQuery("term")));
      }

      [Fact]
      public async Task Throw_ODE_When_AnalyticsQueryAsync_Called_After_Being_Disposed()
      {
          var cluster = CreateClusterFromMocks();

          cluster.Dispose();

          await Assert.ThrowsAsync<ObjectDisposedException>(async ()=> await cluster.AnalyticsQueryAsync<dynamic>("SELECT 1;"));
      }

      [Fact]
      public void Throw_ODE_When_SearchIndexes_Called_After_Being_Disposed()
      {
          var cluster = CreateClusterFromMocks();

          cluster.Dispose();

          Assert.Throws<ObjectDisposedException>(()=> cluster.SearchIndexes);
      }

      [Fact]
      public void Throw_ODE_When_QueryIndexes_Called_After_Being_Disposed()
      {
          var cluster = CreateClusterFromMocks();

          cluster.Dispose();

          Assert.Throws<ObjectDisposedException>(()=> cluster.QueryIndexes);
      }

      [Fact]
      public void Throw_ODE_When_Buckets_Called_After_Being_Disposed()
      {
          var cluster = CreateClusterFromMocks();

          cluster.Dispose();

          Assert.Throws<ObjectDisposedException>(()=> cluster.Buckets);
      }

      internal StellarCluster CreateClusterFromMocks()
      {
          var channel = GrpcChannel.ForAddress(new Uri("https://xxx"));
          var bucketManager = new Mock<IBucketManager>();
          var searchIndexManager = new Mock<ISearchIndexManager>();
          var queryServiceClient = new Mock<QueryService.QueryServiceClient>();
          var analyticsClient = new Mock<IAnalyticsClient>();
          var searchClient = new Mock<IStellarSearchClient>();
          var queryIndexManager = new Mock<IQueryIndexManager>();
          var metaData = new Metadata();
          var requestTracer = new Mock<IRequestTracer>();
          var typeSerializer = new Mock<ITypeSerializer>();
          var clusterOptions = new ClusterOptions();
          var channelCredentials = new ClusterChannelCredentials(clusterOptions);
          var requestOrchestrator = new Mock<IRetryOrchestrator>();

          return new StellarCluster(bucketManager.Object, searchIndexManager.Object, queryIndexManager.Object,
              queryServiceClient.Object, analyticsClient.Object, searchClient.Object, metaData, channelCredentials,
              requestTracer.Object, channel, typeSerializer.Object, requestOrchestrator.Object, clusterOptions);
      }
}
#endif
