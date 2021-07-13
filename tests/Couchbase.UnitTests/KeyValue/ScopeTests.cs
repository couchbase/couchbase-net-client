using System.Diagnostics;
using Couchbase.Core;
using Couchbase.Core.DI;
using Couchbase.KeyValue;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.KeyValue
{
    public class ScopeTests
    {
        [Theory]
        [InlineData("travel-sample", "inventory", "default:`travel-sample`.`inventory`")]
        [InlineData("`travel-sample`", "inventory", "default:`travel-sample`.`inventory`")]
        [InlineData("travel-sample", "`inventory`", "default:`travel-sample`.`inventory`")]
        [InlineData("`travel-sample`", "`inventory`", "default:`travel-sample`.`inventory`")]
        [InlineData("travel-sample`", "inventory", "default:`travel-sample`.`inventory`")]
        [InlineData("travel-sample`", "inventory`", "default:`travel-sample`.`inventory`")]
        [InlineData("`travel-sample`", "`inventory", "default:`travel-sample`.`inventory`")]
        [InlineData("travel-sample`", "`inventory`", "default:`travel-sample`.`inventory`")]
        public void Test_QueryContext(string bucketName, string scopeName, string expectedContext)
        {
            var bucket = new Mock<BucketBase>();
            bucket.Setup(x => x.Name).Returns(bucketName);

            var scope = new Scope(scopeName, bucket.Object, new Mock<ICollectionFactory>().Object,
                new Mock<ILogger<Scope>>().Object);

            Assert.Equal(expectedContext, scope.QueryContext);
        }
    }
}
