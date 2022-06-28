using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core.Retry;
using Couchbase.Query;
using Moq;
using Xunit;

namespace Couchbase.UnitTests
{
    public class ClusterTests
    {
        #region ctor

        [Fact]
        public void ctor_Throws_InvalidConfigurationException_When_Credentials_Not_Provided()
        {
            Assert.Throws<InvalidConfigurationException>(() => new Cluster(new ClusterOptions().WithConnectionString("couchbase://localhost")));
        }

        #endregion

        #region Extensions

        [Theory]
        [InlineData(null, "couchbase://user1@localhost", "user1", null)]
        [InlineData("user1", "couchbase://localhost", "user1", "user1")]
        [InlineData("user1", "couchbase://user2@localhost", "user2", "user1")]
        [InlineData(null, "couchbase://localhost", null, null)]
        [InlineData("", "couchbase://localhost", "", "")]
        [InlineData(" ", "couchbase://bob@localhost", "bob", " ")]
        [InlineData(null, "couchbase://\t@localhost", null, null)]
        public void WithConnectionString_Sets_Username_Unless_Provided(string username, string connectionString, string expectedUserNamePreSet, string expectedUserNamePostSet)
        {
            var clusterOptionsPreSet = new ClusterOptions() {UserName = username}.WithConnectionString(connectionString);
            Assert.Equal(expectedUserNamePreSet, clusterOptionsPreSet.UserName);

            var clusterOptionsPostSet = new ClusterOptions().WithConnectionString(connectionString);

            clusterOptionsPostSet.UserName = username;
            Assert.Equal(expectedUserNamePostSet, clusterOptionsPostSet.UserName);
        }

        #endregion

        #region WaitUntilReady

        [Fact]
        public async Task WaitUntilReady_Throws_NotSupportedException_If_GC3P_Not_Supported()
        {
            var dnsResolver = new Mock<IDnsResolver>();
            var mockNode = new Mock<Couchbase.Core.IClusterNode>();
            var errorContext = new Couchbase.Core.Exceptions.KeyValue.KeyValueErrorContext()
            {
                Status = Couchbase.Core.IO.Operations.ResponseStatus.BucketNotConnected
            };

            mockNode.Setup(n => n.GetClusterMap()).Throws(() => new CouchbaseException(errorContext));
            var nodeFactory = new Mock<Couchbase.Core.DI.IClusterNodeFactory>();
            nodeFactory.Setup(nf => nf.CreateAndConnectAsync(It.IsAny<HostEndpointWithPort>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(mockNode.Object));

            var cluster = new Cluster(ClusterOptions.Default
                .WithCredentials("Administrator", "password")
                .WithConnectionString("couchbases://HostThatDoesNotExist.NoSuchDomain")
                .WithDnsResolver(dnsResolver.Object)
                .AddClusterService<Couchbase.Core.DI.IClusterNodeFactory>(nodeFactory.Object)
                );
            await ((Couchbase.Core.Bootstrapping.IBootstrappable)cluster).BootStrapAsync();
            await Assert.ThrowsAsync<NotSupportedException>(async () =>
                await cluster.WaitUntilReadyAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false)).ConfigureAwait(false);
        }

        #endregion

        #region QueryAsync

        [Fact]
        public async Task QueryAsync_WithObject_Success()
        {
            // Arrange

            var queryResult = new Mock<IQueryResult<TestClass>>();
            queryResult
                .SetupGet(m => m.MetaData)
                .Returns(new QueryMetaData()
                {
                    Status = QueryStatus.Success
                });
            queryResult
                .SetupGet(m => m.RetryReason)
                .Returns(RetryReason.NoRetry);

                var queryClient = new Mock<IQueryClient>();
            queryClient
                .Setup(m => m.QueryAsync<TestClass>(It.IsAny<string>(), It.IsAny<QueryOptions>()))
                .ReturnsAsync(queryResult.Object);

            var options = new ClusterOptions().WithCredentials("u", "p")
                .WithConnectionString("couchbase://localhost");

            var cluster = new Mock<Cluster>(options)
            {
                CallBase = true
            };
            cluster
                .Setup(m => m.EnsureBootstrapped())
                .Returns(Task.CompletedTask);
            cluster.Object.LazyQueryClient = new Lazy<IQueryClient>(() => queryClient.Object);

            // Act

            var result = await cluster.Object.QueryAsync<TestClass>("SELECT * FROM `default`").ConfigureAwait(false);

            // Assert

            Assert.Equal(queryResult.Object, result);
        }

        #endregion

        #region AnalyticsQueryAsync

        [Fact]
        public async Task AnalyticsQueryAsync_WithObject_Success()
        {
            // Arrange

            var analyticsResult = new Mock<IAnalyticsResult<TestClass>>();
            analyticsResult
                .SetupGet(m => m.MetaData)
                .Returns(new AnalyticsMetaData
                {
                    Status = AnalyticsStatus.Success
                });
            analyticsResult
                .SetupGet(m => m.RetryReason)
                .Returns(RetryReason.NoRetry);

            var analyticsClient = new Mock<IAnalyticsClient>();
            analyticsClient
                .Setup(m => m.QueryAsync<TestClass>(It.IsAny<string>(), It.IsAny<AnalyticsOptions>()))
                .ReturnsAsync(analyticsResult.Object);

            var options = new ClusterOptions().WithCredentials("u", "p")
                .WithConnectionString("couchbase://localhost");

            var cluster = new Mock<Cluster>(options)
            {
                CallBase = true
            };
            cluster
                .Setup(m => m.EnsureBootstrapped())
                .Returns(Task.CompletedTask);
            cluster.Object.LazyAnalyticsClient = new Lazy<IAnalyticsClient>(() => analyticsClient.Object);

            // Act

            var result = await cluster.Object.AnalyticsQueryAsync<TestClass>("SELECT * FROM `default`").ConfigureAwait(false);

            // Assert

            Assert.Equal(analyticsResult.Object, result);
        }

        #endregion

        #region Helpers

        public class TestClass
        {
            public string Name { get; set; }
        }

        #endregion
    }
}
