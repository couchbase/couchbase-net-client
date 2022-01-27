using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Transactions.Error.External;
using Couchbase.Transactions.Forwards;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Couchbase.Transactions.Tests.UnitTests.forwards
{
    public class ForwardCompatibilityTests
    {
        [Fact]
        public async Task ProtocolCheckPasses()
        {
            var jsonString = @"{""CL_E"":[{""p"":""2.0"",""b"":""f""}]}";
            var json = JObject.Parse(jsonString);
            await ForwardCompatibility.Check(null, "CL_E", json);
        }

        [Fact]
        public async Task ProtocolCheckFails_NoRetry()
        {
            var jsonString = @"{""CL_E"":[{""p"":""10000.0"",""b"":""f""}]}";
            var json = JObject.Parse(jsonString);
            var t = ForwardCompatibility.Check(null, "CL_E", json);
            var failure = await Assert.ThrowsAsync<TransactionOperationFailedException>(() => t);
            Assert.False(failure.RetryTransaction);
        }

        [Fact]
        public async Task ProtocolCheckFails_WithRetry()
        {
            var jsonString = @"{""CL_E"":[{""p"":""10000.0"",""b"":""r""}]}";
            var json = JObject.Parse(jsonString);
            var t = ForwardCompatibility.Check(null, "CL_E", json);
            var failure = await Assert.ThrowsAsync<TransactionOperationFailedException>(() => t);
            Assert.True(failure.RetryTransaction);
        }

        [Fact]
        public async Task ProtocolCheckPasses_MultipleIp()
        {
            // we're only checking FOO, so CL_E shouldn't cause failure.
            var jsonString = @"{""CL_E"":[{""p"":""1000000.0"",""b"":""f""}],""FOO"":[{""p"":""2.0"",""b"":""f""}]}";
            var json = JObject.Parse(jsonString);
            await ForwardCompatibility.Check(null, "FOO", json);
        }

        [Fact]
        public async Task ProtocolCheckFails_MultipleInteractionPoint()
        {
            var jsonString = @"{""CL_E"":[{""p"":""1000000.0"",""b"":""f""}],""FOO"":[{""p"":""2.0"",""b"":""r""}]}";
            var json = JObject.Parse(jsonString);
            var t = ForwardCompatibility.Check(null, "CL_E", json);
            var failure = await Assert.ThrowsAsync<TransactionOperationFailedException>(() => t);
            Assert.False(failure.RetryTransaction);
        }

        [Fact]
        public async Task ExtensionCheckPasses()
        {
            var jsonString = @"{""CL_E"":[{""e"":""TI"",""b"":""f""}]}";
            var json = JObject.Parse(jsonString);
            var t = ForwardCompatibility.Check(null, "CL_E", json);
            await t;
        }

        [Fact]
        public async Task ExtensionCheckFails_WithRetry()
        {
            var jsonString = @"{""CL_E"":[{""e"":""XXXXXXX"",""b"":""r""}]}";
            var json = JObject.Parse(jsonString);
            var t = ForwardCompatibility.Check(null, "CL_E", json);
            var failure = await Assert.ThrowsAsync<TransactionOperationFailedException>(() => t);
            Assert.True(failure.RetryTransaction);
        }

        [Fact]
        public async Task ExtensionCheckFails_Multiple()
        {
            var jsonString = @"{""CL_E"":[{""e"":""TI"",""b"":""r""}, {""e"":""XX"",""b"":""r""}]}";
            var json = JObject.Parse(jsonString);
            var t = ForwardCompatibility.Check(null, "CL_E", json);
            var failure = await Assert.ThrowsAsync<TransactionOperationFailedException>(() => t);
            Assert.True(failure.RetryTransaction);
        }

        [Fact]
        public async Task ExtensionCheckFailsAfterProtocolPasses()
        {
            var jsonString = @"{""CL_E"":[{""p"":""1.0"",""b"":""f""}, {""e"":""XX"",""b"":""r""}]}";
            var json = JObject.Parse(jsonString);
            var t = ForwardCompatibility.Check(null, "CL_E", json);
            var failure = await Assert.ThrowsAsync<TransactionOperationFailedException>(() => t);
            Assert.True(failure.RetryTransaction);
        }
    }
}
/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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