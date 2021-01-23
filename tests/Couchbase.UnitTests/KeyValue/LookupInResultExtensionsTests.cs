using Couchbase.Core.IO.Serializers;
using Couchbase.KeyValue;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.KeyValue
{
    public class LookupInResultExtensionsTests
    {
        [Fact]
        public void ContentAs_WithExpression_CallsContentWithString()
        {
            // Arrange

            var fakeContent = "abc";

            var fragment = new Mock<ILookupInResult<MyDoc>>();
            fragment.Setup(m => m.ContentAs<string>(It.IsAny<int>())).Returns(fakeContent);
            fragment.Setup(m => m.IndexOf("`prop`")).Returns(1);
            fragment.Setup(m => m.Serializer).Returns(new DefaultSerializer());

            // Act

            var result = fragment.Object.ContentAs(p => p.Prop);

            // Assert

            fragment.Verify(m => m.ContentAs<string>(1), Times.Once);
            Assert.Equal(fakeContent, result);
        }

        [Fact]
        public void Exists_WithExpression_CallsExistsWithString()
        {
            // Arrange

            var fragment = new Mock<ILookupInResult<MyDoc>>();
            fragment.Setup(m => m.Exists(It.IsAny<int>())).Returns(true);
            fragment.Setup(m => m.IndexOf("`prop`")).Returns(1);
            fragment.Setup(m => m.Serializer).Returns(new DefaultSerializer());

            // Act

            var result = fragment.Object.Exists(p => p.Prop);

            // Assert

            fragment.Verify(m => m.Exists(1), Times.Once);
            Assert.True(result);
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
