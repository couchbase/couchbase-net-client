using System;
using Xunit;

namespace Couchbase.UnitTests
{
    public class ConnectionStringTests
    {
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
    }
}
