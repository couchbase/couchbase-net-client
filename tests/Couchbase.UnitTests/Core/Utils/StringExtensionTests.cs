using System;
using Couchbase.Core.Utils;
using Xunit;

namespace Couchbase.UnitTests.Core.Utils
{
    public class StringExtensionTests
    {
        [Theory]
        [InlineData("", "``")]
        [InlineData("default", "`default`")]
        [InlineData("`default", "`default`")]
        [InlineData("default`", "`default`")]
        [InlineData("beer-sample", "`beer-sample`")]
        [InlineData("`beer-sample`", "`beer-sample`")]
        [InlineData("foo`bar", "`foo``bar`")]
        [InlineData("`foo`bar`", "`foo``bar`")]
        [InlineData("`inject``here`", "`inject````here`")]
        public void EscapeIfRequired_Input_ExpectedOutput(string input, string expectedOutput)
        {
            // Act

            var result = input.EscapeIfRequired();

            // Assert

            Assert.Equal(expectedOutput, result);
        }
    }
}
