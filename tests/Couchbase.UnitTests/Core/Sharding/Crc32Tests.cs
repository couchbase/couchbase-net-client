using Couchbase.Core.Sharding;
using Xunit;

namespace Couchbase.UnitTests.Core.Sharding
{
    public class Crc32Tests
    {
        [Theory]
        [InlineData("XXXXX", 13701u)]
        [InlineData("CouchbaseIsAwesome", 25652u)]
        [InlineData("16ByteKeyForEdge", 11333u)]
        [InlineData("32ByteKeyForEdge32ByteKeyForEdge", 30670u)]
        public void ComputeHash_ExpectedResult(string data, uint expectedResult)
        {
            // Arrange

            var buffer = System.Text.Encoding.UTF8.GetBytes(data);

            // Act

            var result = Crc32.ComputeHash(buffer);

            // Assert

            Assert.Equal(expectedResult, result);
        }
    }
}
