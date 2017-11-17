using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.Core
{
    [TestFixture]
    public class NodeAdapterTests
    {
        [Test]
        public void IsAnalyticsNode_Is_True_When_Port_Is_Provided()
        {
            var node = new Node();
            var nodeExt = new NodeExt();
            nodeExt.Hostname = "localhost";
            nodeExt.Services.Analytics = 8095;

            var adapater = new NodeAdapter(node, nodeExt);

            Assert.AreEqual(8095, adapater.Analytics);
            Assert.IsTrue(adapater.IsAnalyticsNode);
        }

        [Test]
        public void IsAnalyticsNodeSsl_Is_True_When_Port_Is_Provided()
        {
            var node = new Node();
            var nodeExt = new NodeExt();
            nodeExt.Hostname = "localhost";
            nodeExt.Services.AnalyticsSsl = 18095;

            var adapater = new NodeAdapter(node, nodeExt);

            Assert.AreEqual(18095, adapater.AnalyticsSsl);
            Assert.IsTrue(adapater.IsAnalyticsNode);
        }

        [Test]
        public void When_IPv6_NodeAdapter_Does_Not_Fail()
        {
            //arrange
            var serverConfigJson = ResourceHelper.ReadResource("config_with_ipv6");
            var serverConfig = JsonConvert.DeserializeObject<BucketConfig>(serverConfigJson);

            //act
            var adapter = new NodeAdapter(serverConfig.Nodes[0], serverConfig.NodesExt[0]);

            //assert
            Assert.IsNotNull(adapter);
        }

        [Test]
        public void When_IPv6_NodeAdapter_GetEndpoint_Succeeds()
        {
            //arrange
            var serverConfigJson = ResourceHelper.ReadResource("config_with_ipv6");
            var serverConfig = JsonConvert.DeserializeObject<BucketConfig>(serverConfigJson);

            var adapter = new NodeAdapter(serverConfig.Nodes[0], serverConfig.NodesExt[0]);

            //act
            var endpoint = adapter.GetIPEndPoint(false);

            //assert
            Assert.IsNotNull(endpoint);
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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

#endregion
