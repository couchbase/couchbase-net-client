using System.Collections.Generic;
using Couchbase.Core.Diagnostics;
using Xunit;

namespace Couchbase.UnitTests.Core.Diagnostics
{
    public class SemanticConventionEmitterTests
    {
        // A mapped key whose modern key is non-empty.
        private const string LegacyMappedKey = "db.system";
        private const string ModernMappedKey = "db.system.name";

        // A mapped key whose modern key is intentionally empty (should not emit modern).
        private const string LegacyMappedKeyWithEmptyModern = "net.host.name";

        [Theory]
        [InlineData(ObservabilitySemanticConvention.Legacy)]
        [InlineData(ObservabilitySemanticConvention.Modern)]
        [InlineData(ObservabilitySemanticConvention.Both)]
        public void EmitAttribute_UnmappedKey_AlwaysEmitsKeyAsIs(ObservabilitySemanticConvention mode)
        {
            var calls = new List<(string Key, string Value)>();

            SemanticConventionEmitter.EmitAttribute(
                mode,
                key: "unmapped.key",
                value: "v",
                state: calls,
                setAttribute: static (s, k, v) => s.Add((k, v)));

            Assert.Single(calls);
            Assert.Equal(("unmapped.key", "v"), calls[0]);
        }

        [Fact]
        public void EmitAttribute_MappedKey_Legacy_EmitsLegacyOnly()
        {
            var calls = new List<(string Key, string Value)>();

            SemanticConventionEmitter.EmitAttribute(
                ObservabilitySemanticConvention.Legacy,
                key: LegacyMappedKey,
                value: "v",
                state: calls,
                setAttribute: static (s, k, v) => s.Add((k, v)));

            Assert.Single(calls);
            Assert.Equal((LegacyMappedKey, "v"), calls[0]);
        }

        [Fact]
        public void EmitAttribute_MappedKey_Modern_EmitsModernOnly()
        {
            var calls = new List<(string Key, string Value)>();

            SemanticConventionEmitter.EmitAttribute(
                ObservabilitySemanticConvention.Modern,
                key: LegacyMappedKey,
                value: "v",
                state: calls,
                setAttribute: static (s, k, v) => s.Add((k, v)));

            Assert.Single(calls);
            Assert.Equal((ModernMappedKey, "v"), calls[0]);
        }

        [Fact]
        public void EmitAttribute_MappedKey_Both_EmitsLegacyThenModern()
        {
            var calls = new List<(string Key, string Value)>();

            SemanticConventionEmitter.EmitAttribute(
                ObservabilitySemanticConvention.Both,
                key: LegacyMappedKey,
                value: "v",
                state: calls,
                setAttribute: static (s, k, v) => s.Add((k, v)));

            Assert.Equal(2, calls.Count);
            Assert.Equal((LegacyMappedKey, "v"), calls[0]);
            Assert.Equal((ModernMappedKey, "v"), calls[1]);
        }

        [Fact]
        public void EmitAttribute_MappedKey_Modern_WithEmptyModernKey_EmitsNothing()
        {
            var calls = new List<(string Key, string Value)>();

            SemanticConventionEmitter.EmitAttribute(
                ObservabilitySemanticConvention.Modern,
                key: LegacyMappedKeyWithEmptyModern,
                value: "v",
                state: calls,
                setAttribute: static (s, k, v) => s.Add((k, v)));

            Assert.Empty(calls);
        }

        [Fact]
        public void EmitAttribute_MappedKey_Both_WithEmptyModernKey_EmitsLegacyOnly()
        {
            var calls = new List<(string Key, string Value)>();

            SemanticConventionEmitter.EmitAttribute(
                ObservabilitySemanticConvention.Both,
                key: LegacyMappedKeyWithEmptyModern,
                value: "v",
                state: calls,
                setAttribute: static (s, k, v) => s.Add((k, v)));

            Assert.Single(calls);
            Assert.Equal((LegacyMappedKeyWithEmptyModern, "v"), calls[0]);
        }

        [Fact]
        public void EmitAttribute_UnknownEnumValue_DefaultsToLegacy()
        {
            var calls = new List<(string Key, string Value)>();

            var invalidMode = (ObservabilitySemanticConvention)999;

            SemanticConventionEmitter.EmitAttribute(
                invalidMode,
                key: LegacyMappedKey,
                value: "v",
                state: calls,
                setAttribute: static (s, k, v) => s.Add((k, v)));

            Assert.Single(calls);
            Assert.Equal((LegacyMappedKey, "v"), calls[0]);
        }

        [Fact]
        public void EmitAttribute_GenericValueType_IsPreserved()
        {
            var calls = new List<(string Key, long Value)>();

            SemanticConventionEmitter.EmitAttribute(
                ObservabilitySemanticConvention.Both,
                key: LegacyMappedKey,
                value: 42L,
                state: calls,
                setAttribute: static (s, k, v) => s.Add((k, v)));

            Assert.Equal(2, calls.Count);
            Assert.Equal((LegacyMappedKey, 42L), calls[0]);
            Assert.Equal((ModernMappedKey, 42L), calls[1]);
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
