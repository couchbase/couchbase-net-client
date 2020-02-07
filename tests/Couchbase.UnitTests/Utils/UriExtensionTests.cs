using System;
using System.Net;
using System.Threading;
using Couchbase.Core.Configuration.Server;
using Couchbase.Utils;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Utils
{
    public class UriExtensionsTests
    {
        #region GetQueryUri

        [Theory]
        [InlineData(true, "10.143.192.101", "https://10.143.192.101:18093/query")]
        [InlineData(false, "10.143.192.101", "http://10.143.192.101:8093/query")]
        public void GetQueryUri_Returns_Query_Uri(bool useSsl, string host, string expectedUri)
        {
            var clusterOptions = new ClusterOptions {EnableTls = useSsl};
            var bucketConfig = ResourceHelper.ReadResource<BucketConfig>(@"Documents\config.json");
            var nodeAdapter = bucketConfig.GetNodes().Find(x => x.Hostname.Equals(host));
            var actual = nodeAdapter.GetQueryUri(clusterOptions);

            var expected = new Uri(expectedUri);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(true, "10.143.192.101", "https://10.143.192.101:18092/")]
        [InlineData(false, "10.143.192.101", "http://10.143.192.101:8092/")]
        public void GetViewUri_Returns_Views_Uri(bool useSsl, string host, string expectedUri)
        {
            var clusterOptions = new  ClusterOptions  {EnableTls = useSsl};
            var bucketConfig = ResourceHelper.ReadResource<BucketConfig>(@"Documents\config.json");
            var nodeAdapter = bucketConfig.GetNodes().Find(x => x.Hostname.Equals(host));
            var actual = nodeAdapter.GetViewsUri(clusterOptions);

            var expected = new Uri(expectedUri);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(true, "10.143.192.101", "https://10.143.192.101:18095/analytics/service")]
        [InlineData(false, "10.143.192.101", "http://10.143.192.101:8095/analytics/service")]
        public void GetAnalyticsUri_Returns_Analytics_Uri(bool useSsl, string host, string expectedUri)
        {
            var clusterOptions = new ClusterOptions {EnableTls = useSsl};
            var bucketConfig = ResourceHelper.ReadResource<BucketConfig>(@"Documents\config.json");
            var nodeAdapter = bucketConfig.GetNodes().Find(x => x.Hostname.Equals(host));
            var actual = nodeAdapter.GetAnalyticsUri(clusterOptions);

            var expected = new Uri(expectedUri);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(true, "10.143.192.101", "https://10.143.192.101:18094")]
        [InlineData(false, "10.143.192.101", "http://10.143.192.101:8094")]
        public void GetSearchUri_Returns_Search_Uri(bool useSsl, string host, string expectedUri)
        {
            var clusterOptions = new ClusterOptions  {EnableTls = useSsl};
            var bucketConfig = ResourceHelper.ReadResource<BucketConfig>(@"Documents\config.json");
            var nodeAdapter = bucketConfig.GetNodes().Find(x => x.Hostname.Equals(host));
            var actual = nodeAdapter.GetSearchUri(clusterOptions);

            var expected = new Uri(expectedUri);

            Assert.Equal(expected, actual);
        }

        #endregion

        #region GetIpAddress

        [Fact]
        public void GetIpAddress_DnsEntry_ReturnsIp()
        {
            // Arrange

            var uri = new Uri("http://localhost/");

            // Act

            var result = uri.GetIpAddress(false);

            // Assert

            Assert.Equal(new IPAddress(new byte[] {127, 0, 0, 1}), result);
        }

        [Fact]
        public void GetIpAddress_DnsEntry_NoDeadlock()
        {
            // Using an asynchronous view query within an MVC Web API action causes
            // a deadlock if you wait for the result synchronously.

            var context = new Mock<SynchronizationContext>
            {
                CallBase = true
            };

            SynchronizationContext.SetSynchronizationContext(context.Object);
            try
            {
                var uri = new Uri("http://localhost/");

                uri.GetIpAddress(false);

                // If view queries are incorrectly awaiting on the current SynchronizationContext
                // We will see calls to Post or Send on the mock

                context.Verify(m => m.Post(It.IsAny<SendOrPostCallback>(), It.IsAny<object>()), Times.Never);
                context.Verify(m => m.Send(It.IsAny<SendOrPostCallback>(), It.IsAny<object>()), Times.Never);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        #endregion

       /* #region ReplaceCouchbaseSchemeWithHttp

        [Theory]
        [InlineData(true, "https")]
        [InlineData(false, "http")]
        public void ReplaceCouchbaseSchemeWithHttp_Root_Configuration(bool useSsl, string expectedScheme)
        {
            //arrange
            var config = new Configuration
            {
                EnableTLS = useSsl
            };
            var uri = new UriBuilder("couchbase", "localhost", 8091).Uri;

            //act
            var actualSceheme = uri.ReplaceCouchbaseSchemeWithHttp(config, "travel-sample").Scheme;

            //assert
            Assert.Equal(expectedScheme, actualSceheme);
        }

        [Theory]
        [InlineData(true, "https")]
        [InlineData(false, "http")]
        public void ReplaceCouchbaseSchemeWithHttp_Bucket_Configuration(bool useSsl, string expectedScheme)
        {
            //arrange
            var config = new ClientConfiguration
            {
               BucketConfigs = new Dictionary<string, BucketConfiguration>
               {
                   {"travel-sample", new BucketConfiguration
                   {
                       EnableTLS = useSsl
                   } }
               }
            };
            var uri = new UriBuilder("couchbase", "localhost", 8091).Uri;

            //act
            var actualScheme = uri.ReplaceCouchbaseSchemeWithHttp(config, "travel-sample").Scheme;

            //assert
            Assert.Equal(expectedScheme, actualScheme);
        }

        [Theory]
        [InlineData(8091)]
        [InlineData(12345)]
        public void ReplaceCouchbaseSchemeWithHttp_sets_port_to_config_management_port(int expectedPort)
        {
            //arrange
            var config = new Configuration {MgmtPort = expectedPort };
            var uri = new UriBuilder("couchbase", "localhost").Uri;

            //act
            var actualPort = uri.ReplaceCouchbaseSchemeWithHttp(config, "travel-sample").Port;

            //assert
            Assert.AreEqual(expectedPort, actualPort);
        }

        #endregion */
    }
}
