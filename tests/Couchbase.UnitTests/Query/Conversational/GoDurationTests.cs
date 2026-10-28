using System;
using Couchbase.Query.Conversational;
using Xunit;

#nullable enable

namespace Couchbase.UnitTests.Query.Conversational
{
    public class GoDurationTests
    {
        [Theory]
        [InlineData("0", 0L)]
        [InlineData("-0", 0L)]
        [InlineData("+0", 0L)]
        [InlineData("0s", 0L)]
        [InlineData("1.983639787s", 19_836_398L)]
        [InlineData("2.825383895s", 28_253_839L)]
        [InlineData("6.282322ms", 62_823L)]
        [InlineData("85.322us", 853L)]
        [InlineData("85.322µs", 853L)]
        [InlineData("85.322μs", 853L)]
        [InlineData("1m20.5s", 805_000_000L)]
        [InlineData("1h2m3s", 37_230_000_000L)]
        [InlineData("1.5h", 54_000_000_000L)]
        [InlineData("-1.5h", -54_000_000_000L)]
        [InlineData("+5s", 50_000_000L)]
        [InlineData(".5s", 5_000_000L)]
        [InlineData("1.s", 10_000_000L)]
        [InlineData("100ns", 1L)]
        [InlineData("149ns", 1L)]
        [InlineData("150ns", 2L)]
        [InlineData("49ns", 0L)]
        [InlineData("1s500ms", 15_000_000L)]
        public void TryParse_Valid_ReturnsRoundedTicks(string value, long expectedTicks)
        {
            Assert.True(GoDuration.TryParse(value, out var result));
            Assert.Equal(TimeSpan.FromTicks(expectedTicks), result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("-")]
        [InlineData("+")]
        [InlineData("s")]
        [InlineData("1")]
        [InlineData("1.5")]
        [InlineData("00")]
        [InlineData(".s")]
        [InlineData("1.2.3s")]
        [InlineData("1x")]
        [InlineData("1sec")]
        [InlineData("1S")]
        [InlineData("1µ")]
        [InlineData(" 1s")]
        [InlineData("1s ")]
        [InlineData("1 s")]
        [InlineData("--1s")]
        [InlineData("1h-2m")]
        [InlineData("1,5s")]
        [InlineData("99999999999999999999h")]
        public void TryParse_Invalid_ReturnsFalse(string? value)
        {
            Assert.False(GoDuration.TryParse(value, out var result));
            Assert.Equal(TimeSpan.Zero, result);
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2026 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
