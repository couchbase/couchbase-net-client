using System.Collections.Generic;
using System.Text.Json;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Xunit;

#nullable enable

namespace Couchbase.UnitTests.Query.Conversational;

public class ConversationalQueryCapabilityTests
{
    [Fact]
    public void HasClusterCap_ConversationalQueryAdvertised_ReturnsTrue()
    {
        var config = new BucketConfig
        {
            ClusterCapabilities = new Dictionary<string, IEnumerable<string>>
            {
                ["n1ql"] = new[] { "enhancedPreparedStatements", "readFromReplica", "conversationalQuery" }
            }
        };

        Assert.True(config.HasClusterCap(ClusterCapabilities.CONVERSATIONAL_QUERY));
    }

    [Fact]
    public void HasClusterCap_ConversationalQueryInOtherSection_ReturnsFalse()
    {
        var config = new BucketConfig
        {
            ClusterCapabilities = new Dictionary<string, IEnumerable<string>>
            {
                ["n1ql"] = new[] { "enhancedPreparedStatements", "readFromReplica" },
                ["search"] = new[] { "conversationalQuery" }
            }
        };

        Assert.False(config.HasClusterCap(ClusterCapabilities.CONVERSATIONAL_QUERY));
    }

    [Fact]
    public void HasClusterCap_NoCapabilities_ReturnsFalse()
    {
        Assert.False(new BucketConfig().HasClusterCap(ClusterCapabilities.CONVERSATIONAL_QUERY));
    }

    [Theory]
    [InlineData("""{"clusterCapabilities":{"n1ql":["enhancedPreparedStatements","conversationalQuery"]}}""", true)]
    [InlineData("""{"clusterCapabilities":{"n1ql":["enhancedPreparedStatements"]}}""", false)]
    [InlineData("""{"clusterCapabilitiesVer":[1,0]}""", false)]
    public void HasClusterCap_FromConfigJson(string json, bool expected)
    {
        var config = JsonSerializer.Deserialize(json, InternalSerializationContext.Default.BucketConfig)!;

        Assert.Equal(expected, config.HasClusterCap(ClusterCapabilities.CONVERSATIONAL_QUERY));
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
