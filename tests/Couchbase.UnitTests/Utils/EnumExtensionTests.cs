using System;
using System.ComponentModel;
using Couchbase.Utils;
using Xunit;

namespace Couchbase.UnitTests.Utils
{
    public class EnumExtensionTests
    {
        #region GetDescription

        [Fact]
        public void GetDescription_Null_ReturnsNull()
        {
            // Arrange

            TestEnum? value = null;

            // Act

            var result = value.GetDescription();

            // Assert

            Assert.Null(result);
        }

        [Fact]
        public void GetDescription_UndefinedValue_ReturnsNull()
        {
            // Arrange

            TestEnum? value = (TestEnum)(-1);

            // Act

            var result = value.GetDescription();

            // Assert

            Assert.Null(result);
        }

        [Theory]
        [InlineData(TestEnum.None, null)]
        [InlineData(TestEnum.A, null)]
        [InlineData(TestEnum.B, "Other")]
        public void GetDescription_DefinedValue_ReturnsDescription(TestEnum value, string expectedResult)
        {
            // Act

            var result = value.GetDescription();

            // Assert

            Assert.Equal(expectedResult, result);
        }

        #endregion

        #region TryGetFromDescription

        [Fact]
        public void TryGetFromDescription_UndefinedValue_ReturnsEnum()
        {
            // Act

            var result = EnumExtensions.TryGetFromDescription<TestEnum>("Other", out var value);

            // Assert

            Assert.True(result);
            Assert.Equal(TestEnum.B, value);
        }

        [Fact]
        public void TryGetFromDescription_DefinedValue_False()
        {
            // Act

            var result = EnumExtensions.TryGetFromDescription<TestEnum>("C", out _);

            // Assert

            Assert.False(result);
        }

        #endregion

        public enum TestEnum
        {
            None = 0,
            A = 1,
            [Description("Other")]
            B = 3,
        }
    }
}
