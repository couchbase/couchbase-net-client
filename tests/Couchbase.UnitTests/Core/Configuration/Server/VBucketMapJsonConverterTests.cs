using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Couchbase.Core.Sharding;
using Xunit;

namespace Couchbase.UnitTests.Core.Configuration.Server
{
    public class VBucketMapJsonConverterTests
    {
        [Theory]
        [InlineData(5)]
        [InlineData(1500)]
        public void Deserialize_MixedLengths_Success(int length)
        {
            // Arrange

            var array = Enumerable.Range(1, length)
                .Select((p, index) =>
                {
                    var arr = new short[index % 4 + 1];
                    for (var i = 0; i < arr.Length; i++)
                    {
                        arr[i] = (short) i;
                    }

                    return arr;
                })
                .ToArray();

            var json = JsonSerializer.Serialize(array);

            // Act

            var result = JsonSerializer.Deserialize<short[][]>(json, new JsonSerializerOptions
            {
                Converters =
                {
                    new VBucketMapJsonConverter()
                }
            });

            // Assert

            Assert.NotNull(result);
            Assert.Equal(length, result.Length);

            var index = 0;
            foreach (var row in result)
            {
                Assert.Equal(index % 4 + 1, row.Length);
                index++;
            }
        }
    }
}
