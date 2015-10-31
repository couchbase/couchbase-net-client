using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;
using Couchbase.Tests.Fakes;
using Couchbase.Utils;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.Tests.Configuration
{
    [TestFixture]
    // ReSharper disable once InconsistentNaming - MDS == Multi Dimensional Scaling
    public  class CouchbaseConfigContext_MDS_Tests
    {
        private CouchbaseConfigContext _configContext;

        [TestFixtureSetUp]
        public void SetUp()
        {
            var configuration = new ClientConfiguration
            {
                UseSsl = true
            };
            configuration.Initialize();

            var json = File.ReadAllText(@"Data\\Configuration\\cb4-config-4-nodes.json");
            var config = JsonConvert.DeserializeObject<BucketConfig>(json);
            var nodes = config.GetNodes();

            var node = nodes.Find(x => x.Hostname.Equals("192.168.109.104"));

            var ioStrategy = new FakeIOStrategy(UriExtensions.GetEndPoint(node.Hostname + ":" + node.KeyValue),
                new FakeConnectionPool(), false);

            _configContext = new CouchbaseConfigContext(config,
                configuration,
                pool => ioStrategy,
                (c, e) => new FakeConnectionPool(),
                SaslFactory.GetFactory(),
                new DefaultTranscoder(new DefaultConverter()));

            _configContext.LoadConfig();
        }

        [Test]
        public void When_Config_Has_One_Query_Node_QueryNodes_Is_One()
        {
            var type = typeof(CouchbaseConfigContext);
            var nodes = (List<IServer>)type.InvokeMember("QueryNodes",
                BindingFlags.GetField | BindingFlags.Instance | BindingFlags.NonPublic,
                null, _configContext, null);
            Assert.AreEqual(1, nodes.Count);
        }

        [Test]
        public void When_Config_Has_Two_View_Nodes_ViewNodes_Is_Two()
        {
            var type = typeof(CouchbaseConfigContext);
            var nodes = (List<IServer>)type.InvokeMember("ViewNodes",
                BindingFlags.GetField | BindingFlags.Instance | BindingFlags.NonPublic,
                null, _configContext, null);
            Assert.AreEqual(2, nodes.Count);
        }

        [Test]
        public void When_Config_Has_Two_Data_Nodes_DataNodes_Is_Two()
        {
            var type = typeof(CouchbaseConfigContext);
            var nodes = (List<IServer>)type.InvokeMember("DataNodes",
                BindingFlags.GetField | BindingFlags.Instance | BindingFlags.NonPublic,
                null, _configContext, null);
            Assert.AreEqual(2, nodes.Count);
        }

        [Test]
        public void When_Config_Has_One_Index_Node_IndexNodes_Is_One()
        {
            var type = typeof(CouchbaseConfigContext);
            var nodes = (List<IServer>)type.InvokeMember("IndexNodes",
                BindingFlags.GetField | BindingFlags.Instance | BindingFlags.NonPublic,
                null, _configContext, null);
            Assert.AreEqual(1, nodes.Count);
        }

        [Test]
        public void When_GetDataNode_Is_Called_A_Data_Node_Is_Returned()
        {
            var node = _configContext.GetDataNode();
            Assert.IsTrue(node.IsDataNode);
        }

        [Test]
        public void When_GetQueryNode_Is_Called_A_Query_Node_Is_Returned()
        {
            var node = _configContext.GetQueryNode();
            Assert.IsTrue(node.IsQueryNode);
        }

        [Test]
        public void When_GetIndexNode_Is_Called_A_Index_Node_Is_Returned()
        {
            var node = _configContext.GetIndexNode();
            Assert.IsTrue(node.IsIndexNode);
        }

        [Test]
        public void When_GetViewNode_Is_Called_A_View_Node_Is_Returned()
        {
            var node = _configContext.GetViewNode();
            Assert.IsTrue(node.IsViewNode);
        }

        [Test]
        public void When_A_Node_In_The_Cluster_Supports_Data_IsDataCapable_Returns_True()
        {
            Assert.IsTrue(_configContext.IsDataCapable);
        }

        [Test]
        public void When_A_Node_In_The_Cluster_Supports_Query_IsQueryCapable_Returns_True()
        {
            Assert.IsTrue(_configContext.IsQueryCapable);
        }


        [Test]
        public void When_A_Node_In_The_Cluster_Supports_Views_IsViewCapable_Returns_True()
        {
            Assert.IsTrue(_configContext.IsViewCapable);
        }

        [Test]
        public void When_A_Node_In_The_Cluster_Supports_Index_IsIndexCapable_Returns_True()
        {
            Assert.IsTrue(_configContext.IsIndexCapable);
        }
    }
}
