using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Utils;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.Tests.Utils
{
    [TestFixture]
// ReSharper disable once InconsistentNaming
    public class IPEndpointExtensionsTests
    {
        [Test]
        public void When_NodeExt_And_UseSsl_Is_True_IPEndpoint_Uses_Port_11207()
        {
            var serverConfigJson = File.ReadAllText("Data\\Configuration\\config-with-nodes-ext.json");
            var serverConfig = JsonConvert.DeserializeObject<BucketConfig>(serverConfigJson);
            var clientConfig = new BucketConfiguration { UseSsl = true };

            const string expected = "192.168.56.101:11207";
            var actual = IPEndPointExtensions.GetEndPoint(serverConfig.NodesExt[0], clientConfig, serverConfig);
            Assert.AreEqual(expected, actual.ToString());
        }

        [Test]
        public void When_NodeExt_And_UseSsl_Is_False_IPEndpoint_Uses_Port_11210()
        {
            var serverConfigJson = File.ReadAllText("Data\\Configuration\\config-with-nodes-ext.json");
            var serverConfig = JsonConvert.DeserializeObject<BucketConfig>(serverConfigJson);
            var clientConfig = new BucketConfiguration { UseSsl = false };

            const string expected = "192.168.56.101:11210";
            var actual = IPEndPointExtensions.GetEndPoint(serverConfig.NodesExt[0], clientConfig, serverConfig);
            Assert.AreEqual(expected, actual.ToString());
        }

        [Test]
        public void When_Node_And_UseSsl_Is_True_IPEndPoint_Uses_Port_11207()
        {
            var serverConfigJson = File.ReadAllText("Data\\Configuration\\config-with-nodes-ext.json");
            var serverConfig = JsonConvert.DeserializeObject<BucketConfig>(serverConfigJson);
            var clientConfig = new BucketConfiguration { UseSsl = true };

            const string expected = "192.168.56.101:11207";
            var actual = IPEndPointExtensions.GetEndPoint(serverConfig.Nodes[0], clientConfig, serverConfig);
            Assert.AreEqual(expected, actual.ToString());
        }

        [Test]
        public void When_Node_And_UseSsl_Is_True_IPEndPoint_Uses_Port_11210()
        {
            var serverConfigJson = File.ReadAllText("Data\\Configuration\\config-with-nodes-ext.json");
            var serverConfig = JsonConvert.DeserializeObject<BucketConfig>(serverConfigJson);
            var clientConfig = new BucketConfiguration { UseSsl = false};

            const string expected = "192.168.56.101:11210";
            var actual = IPEndPointExtensions.GetEndPoint(serverConfig.Nodes[0], clientConfig, serverConfig);
            Assert.AreEqual(expected, actual.ToString());
        }

        [Test]
        public void When_NodeAdapter_And_UseSsl_Is_True_IPEndPoint_Uses_Port_11207()
        {
            var serverConfigJson = File.ReadAllText("Data\\Configuration\\config-with-nodes-ext.json");
            var serverConfig = JsonConvert.DeserializeObject<BucketConfig>(serverConfigJson);
            var clientConfig = new BucketConfiguration { UseSsl = true };

            const string expected = "192.168.56.101:11207";
            var actual = IPEndPointExtensions.GetEndPoint(serverConfig.GetNodes()[0], clientConfig, serverConfig);
            Assert.AreEqual(expected, actual.ToString());
        }

        [Test]
        public void When_NodeAdapter_And_UseSsl_Is_True_IPEndPoint_Uses_Port_11210()
        {
            var serverConfigJson = File.ReadAllText("Data\\Configuration\\config-with-nodes-ext.json");
            var serverConfig = JsonConvert.DeserializeObject<BucketConfig>(serverConfigJson);
            var clientConfig = new BucketConfiguration { UseSsl = false };

            const string expected = "192.168.56.101:11210";
            var actual = IPEndPointExtensions.GetEndPoint(serverConfig.GetNodes()[0], clientConfig, serverConfig);
            Assert.AreEqual(expected, actual.ToString());
        }
    }
}
