using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Query;
using Moq;
using Xunit;

namespace Couchbase.UnitTests
{
    public class ClusterExtensionsTests
    {
        #region QueryInterpolatedAsync

        #if NET6_0_OR_GREATER

        [Fact]
        public async Task QueryInterpolatedAsync_NoOptions_BuildsQuery()
        {
            // Arrange

            QueryOptions options = null;
            var cluster = CreateMockQueryCapturingCluster<dynamic>(queryOptions =>
            {
                options = queryOptions;
            });

            // Act

            var type = "person";
            var limit = 10;
            await cluster.QueryInterpolatedAsync<dynamic>(
                $"SELECT x FROM default WHERE type = {type} LIMIT {limit}");

            // Assert

            Assert.NotNull(options);
            Assert.False(options.IsAdHoc);

            Assert.Equal("SELECT x FROM default WHERE type = $1 LIMIT $2", options.StatementValue);

            Assert.True(options.GetFormValues().TryGetValue("args", out var args));
            var argsArray = Assert.IsType<List<object>>(args);
            Assert.Equal(2, argsArray.Count);
            Assert.Equal(type, argsArray[0]);
            Assert.Equal(limit, argsArray[1]);
        }

        [Fact]
        public async Task QueryInterpolatedAsync_IncomingOptions_BuildsQuery()
        {
            // Arrange

            var options = new QueryOptions().ScanConsistency(QueryScanConsistency.RequestPlus);

            QueryOptions forwardedOptions = null;
            var cluster = CreateMockQueryCapturingCluster<dynamic>(queryOptions =>
            {
                forwardedOptions = queryOptions;
            });

            // Act

            var type = "person";
            var limit = 10;
            await cluster.QueryInterpolatedAsync<dynamic>(options,
                $"SELECT x FROM default WHERE type = {type} LIMIT {limit}");

            // Assert

            Assert.Same(options, forwardedOptions);

            Assert.Equal("SELECT x FROM default WHERE type = $1 LIMIT $2", options.StatementValue);

            Assert.True(options.GetFormValues().TryGetValue("args", out var args));
            var argsArray = Assert.IsType<List<object>>(args);
            Assert.Equal(2, argsArray.Count);
            Assert.Equal(type, argsArray[0]);
            Assert.Equal(limit, argsArray[1]);
        }

        [Fact]
        public async Task QueryInterpolatedAsync_OptionsAction_BuildsQuery()
        {
            // Arrange
            QueryOptions options = null;
            var cluster = CreateMockQueryCapturingCluster<dynamic>(queryOptions =>
            {
                options = queryOptions;
            });

            // Act

            var type = "person";
            var limit = 10;
            await cluster.QueryInterpolatedAsync<dynamic>(o => o.Timeout(TimeSpan.FromMinutes(1)),
                $"SELECT x FROM default WHERE type = {type} LIMIT {limit}");

            // Assert

            Assert.NotNull(options);
            Assert.False(options.IsAdHoc);
            Assert.Equal(TimeSpan.FromMinutes(1), options.TimeoutValue);

            Assert.Equal("SELECT x FROM default WHERE type = $1 LIMIT $2", options.StatementValue);

            Assert.True(options.GetFormValues().TryGetValue("args", out var args));
            var argsArray = Assert.IsType<List<object>>(args);
            Assert.Equal(2, argsArray.Count);
            Assert.Equal(type, argsArray[0]);
            Assert.Equal(limit, argsArray[1]);
        }

        private ICluster CreateMockQueryCapturingCluster<T>(Action<QueryOptions> queryExecution)
        {
            var cluster = new Mock<ICluster>();
            cluster
                .Setup(m => m.QueryAsync<T>(It.IsAny<string>(), It.IsAny<QueryOptions>()))
                .ReturnsAsync((string query, QueryOptions options) =>
                {
                    options.Statement(query);
                    queryExecution(options);

                    return Mock.Of<IQueryResult<T>>();
                });

            return cluster.Object;
        }

        #endif

        #endregion
    }
}
