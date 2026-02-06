#nullable enable
using Couchbase.Core.Diagnostics;
using Xunit;

namespace Couchbase.UnitTests.Core.Diagnostics
{
    public class ObservabilitySemanticConventionParserTests
    {
        [Theory]
        [InlineData(null, ObservabilitySemanticConvention.Legacy)]
        [InlineData("", ObservabilitySemanticConvention.Legacy)]
        [InlineData("   ", ObservabilitySemanticConvention.Legacy)]
        [InlineData(",", ObservabilitySemanticConvention.Legacy)]
        [InlineData(",,", ObservabilitySemanticConvention.Legacy)]
        [InlineData("foo", ObservabilitySemanticConvention.Legacy)]
        [InlineData("foo,bar", ObservabilitySemanticConvention.Legacy)]
        public void Parse_ReturnsLegacy_WhenUnsetEmptyOrNoRecognizedTokens(
            string? raw,
            ObservabilitySemanticConvention expected)
        {
            var actual = ObservabilitySemanticConventionParser.Parse(raw);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("database", ObservabilitySemanticConvention.Modern)]
        [InlineData("DATABASE", ObservabilitySemanticConvention.Modern)]
        [InlineData(" database ", ObservabilitySemanticConvention.Modern)]
        [InlineData("foo,database,bar", ObservabilitySemanticConvention.Modern)]
        [InlineData("foo, database ,bar", ObservabilitySemanticConvention.Modern)]
        public void Parse_ReturnsStandard_WhenDatabaseTokenPresent(
            string raw,
            ObservabilitySemanticConvention expected)
        {
            var actual = ObservabilitySemanticConventionParser.Parse(raw);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("database/dup", ObservabilitySemanticConvention.Both)]
        [InlineData("DATABASE/DUP", ObservabilitySemanticConvention.Both)]
        [InlineData(" database/dup ", ObservabilitySemanticConvention.Both)]
        [InlineData("foo,database/dup,bar", ObservabilitySemanticConvention.Both)]
        [InlineData("database,database/dup", ObservabilitySemanticConvention.Both)]
        [InlineData("database/dup,database", ObservabilitySemanticConvention.Both)]
        public void Parse_ReturnsBoth_WhenDatabaseDupTokenPresent(
            string raw,
            ObservabilitySemanticConvention expected)
        {
            var actual = ObservabilitySemanticConventionParser.Parse(raw);

            Assert.Equal(expected, actual);
        }

        [Theory]
        // Empty entries should be ignored, trimming should work.
        [InlineData(" , database , ", ObservabilitySemanticConvention.Modern)]
        [InlineData(" , database/dup , ", ObservabilitySemanticConvention.Both)]
        [InlineData(" , , database , , ", ObservabilitySemanticConvention.Modern)]
        [InlineData(" , , database/dup , , ", ObservabilitySemanticConvention.Both)]
        public void Parse_IgnoresEmptyEntries_AndTrimsTokens(
            string raw,
            ObservabilitySemanticConvention expected)
        {
            var actual = ObservabilitySemanticConventionParser.Parse(raw);

            Assert.Equal(expected, actual);
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
