using System;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Utils;
using Xunit;

namespace Couchbase.UnitTests.Utils
{
    public class UriExtensionsTests
    {
        #region GetQueryUri

        [Theory]
        [InlineData(true, "10.143.192.101", "https://10.143.192.101:18093/query/service")]
        [InlineData(false, "10.143.192.101", "http://10.143.192.101:8093/query/service")]
        public void GetQueryUri_Returns_Query_Uri(bool useSsl, string host, string expectedUri)
        {
            var clusterOptions = new ClusterOptions {EnableTls = useSsl};
            var bucketConfig = ResourceHelper.ReadResource(@"Documents\config.json",
                InternalSerializationContext.Default.BucketConfig);
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
            var bucketConfig = ResourceHelper.ReadResource(@"Documents\config.json",
                InternalSerializationContext.Default.BucketConfig);
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
            var bucketConfig = ResourceHelper.ReadResource(@"Documents\config.json",
                InternalSerializationContext.Default.BucketConfig);
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
            var bucketConfig = ResourceHelper.ReadResource(@"Documents\config.json",
                InternalSerializationContext.Default.BucketConfig);
            var nodeAdapter = bucketConfig.GetNodes().Find(x => x.Hostname.Equals(host));
            var actual = nodeAdapter.GetSearchUri(clusterOptions);

            var expected = new Uri(expectedUri);

            Assert.Equal(expected, actual);
        }

        #endregion
    }
}
