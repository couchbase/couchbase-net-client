using System;
using Xunit;

namespace Couchbase.UnitTests
{
    public class ConnectionStringTests
    {
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
    }
}
