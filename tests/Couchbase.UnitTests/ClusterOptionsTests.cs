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
    }
}
