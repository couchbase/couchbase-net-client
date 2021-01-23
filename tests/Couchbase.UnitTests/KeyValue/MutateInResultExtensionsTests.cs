using Couchbase.Core.IO.Serializers;
using Couchbase.KeyValue;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.KeyValue
{
    public class MutateInResultExtensionsTests
    {
        [Fact]
        public void ContentAs_WithExpression_CallsContentWithString()
        {
            // Arrange

            var fakeContent = "abc";

            var fragment = new Mock<IMutateInResult<MyDoc>>();
            fragment.Setup(m => m.ContentAs<string>(It.IsAny<int>())).Returns(fakeContent);
            fragment.Setup(m => m.IndexOf("`prop`")).Returns(1);
            fragment.Setup(m => m.Serializer).Returns(new DefaultSerializer());

            // Act

            var result = fragment.Object.ContentAs(p => p.Prop);

            // Assert

            fragment.Verify(m => m.ContentAs<string>(1), Times.Once);
            Assert.Equal(fakeContent, result);
        }

        #region Helpers

        public class MyDoc
        {
            public string Prop { get; set; }
            public string[] Array { get; set; }
            public dynamic Dynamic { get; set; }
        }

        #endregion
    }
}
