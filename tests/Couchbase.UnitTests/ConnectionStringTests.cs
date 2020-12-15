using System;
using Xunit;

namespace Couchbase.UnitTests
{
    public class ConnectionStringTests
    {
        [Theory]
        [InlineData("couchbase://,localhost")]
        [InlineData("couchbase://localhost,")]
        [InlineData("couchbase://localhost1,,localhost")]
        public void Empty_Host_Throws_ArgumentNullException(string connectionString)
        {
            Assert.Throws<ArgumentNullException>(() => ConnectionString.Parse(connectionString));
        }
        #region IsValidDnsSrv

        [Theory]
        [InlineData("couchbase://localhost", true)]
        [InlineData("couchbases://localhost", true)]
        [InlineData("http://localhost", false)]
        // multiple hosts not allowed
        [InlineData("couchbase://localhost,localhost2", false)]
        [InlineData("couchbases://localhost,localhost2", false)]
        [InlineData("http://localhost,localhost2", false)]
        // specified port not allowed
        [InlineData("couchbase://localhost:10012", false)]
        [InlineData("couchbases://localhost:10012", false)]
        [InlineData("http://localhost:10012", false)]
        public void IsValidDnsSrv_IsExpectedResult(string connStr, bool expected)
        {
            var connectionString = ConnectionString.Parse(connStr);

            Assert.Equal(expected, connectionString.IsValidDnsSrv());
        }

        #endregion

        #region ToString

        [Theory]
        [InlineData("couchbase://localhost")]
        [InlineData("couchbase://localhost?param=value")]
        [InlineData("couchbases://localhost")]
        [InlineData("http://localhost")]
        [InlineData("couchbase://localhost,localhost2")]
        [InlineData("couchbases://localhost,localhost2")]
        [InlineData("http://localhost,localhost2")]
        [InlineData("couchbase://localhost:10012")]
        [InlineData("couchbases://localhost:10012")]
        [InlineData("http://localhost:10012")]
        [InlineData("couchbase://username@localhost?param=value")]
        public void ToString_ExpectedResult(string input)
        {
            // Arrange

            var connectionString = ConnectionString.Parse(input);

            // Act

            var result = connectionString.ToString();

            // Assert

            Assert.Equal(input, result);
        }

        #endregion

        #region Parameters
        [Fact]
        public void Test_Parameters()
        {
            var cstring = "couchbase://localhost?kv_connect_timeout=1000&kv_timeout=1001&kv_durable_timeout=1002" +
                          "&view_timeout=1003&query_timeout=1004&analytics_timeout=1005&search_timeout=1006" +
                          "&management_timeout=1007&enable_tls=true&enable_mutation_tokens=true&tcp_keepalive_time=1000" +
                          "&enable_tcp_keepalives=true&force_ipv4=true&config_poll_interval=1008&config_poll_floor_interval=1000" +
                          "&config_idle_redial_timeout=1009&num_kv_connections=10&max_kv_connections=20&max_http_connections=5"+
                          "&idle_http_connection_timeout=1000&enable_config_polling=true&compression=off&compression_min_size=512" +
                          "&compression_min_ratio=0.50";

            var options = new ClusterOptions
            {
                ConnectionString = cstring
            };

            Assert.Equal(TimeSpan.FromMilliseconds(1000), options.KvConnectTimeout);
            Assert.Equal(TimeSpan.FromMilliseconds(1001), options.KvTimeout);
            Assert.Equal(TimeSpan.FromMilliseconds(1002), options.KvDurabilityTimeout);
            Assert.Equal(TimeSpan.FromMilliseconds(1003), options.ViewTimeout);
            Assert.Equal(TimeSpan.FromMilliseconds(1004), options.QueryTimeout);
            Assert.Equal(TimeSpan.FromMilliseconds(1005), options.AnalyticsTimeout);
            Assert.Equal(TimeSpan.FromMilliseconds(1006), options.SearchTimeout);
            Assert.Equal(TimeSpan.FromMilliseconds(1007), options.ManagementTimeout);
            Assert.Equal(true, options.EnableTls);
            Assert.True(options.EnableMutationTokens);
            Assert.Equal(TimeSpan.FromMilliseconds(1000), options.TcpKeepAliveTime);
            Assert.Equal(TimeSpan.FromMilliseconds(1000), options.TcpKeepAliveInterval);
            Assert.True(options.EnableTcpKeepAlives);
            Assert.True(options.ForceIPv4);
            Assert.Equal(TimeSpan.FromMilliseconds(1008), options.ConfigPollInterval);
            Assert.Equal(TimeSpan.FromMilliseconds(1000), options.ConfigPollFloorInterval);
            Assert.Equal(TimeSpan.FromMilliseconds(1009), options.ConfigIdleRedialTimeout);
            Assert.Equal(10, options.NumKvConnections);
            Assert.Equal(20, options.MaxKvConnections);
            Assert.Equal(5, options.MaxHttpConnections);
            Assert.True(options.EnableConfigPolling);
            Assert.False(options.Compression);
            Assert.Equal(512, options.CompressionMinSize);
            Assert.Equal(0.50f, options.CompressionMinRatio);
        }
        #endregion
    }
}
