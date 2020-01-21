using System;
using Xunit;

namespace Couchbase.UnitTests
{
    public class ClusterOptionsTests
    {
        [Fact]
        public void Test_ConfigPollInterval_Default_Is_2_5Seconds()
        {
            var options =  new ClusterOptions();
            Assert.Equal(TimeSpan.FromSeconds(2.5), options.ConfigPollInterval);
        }

        [Fact]
        public void Test_EnableConfigPolling_Default_Is_True()
        {
            var options = new ClusterOptions();
            Assert.True(options.EnableConfigPolling);
        }

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
        public void TestValidConnectionStrings(string connstr, bool expected)
        {
            var connectionString = ConnectionString.Parse(connstr);
            var options = new ClusterOptions
            {
                ConnectionStringValue = connectionString
            };
            Assert.Equal(expected, options.IsValidDnsSrv());
        }
    }
}
