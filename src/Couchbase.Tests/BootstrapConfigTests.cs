using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using Couchbase.Configuration;
using Couchbase.Exceptions;
using NUnit.Framework;

namespace Couchbase.Tests
{
    [TestFixture]
    public class BootstrapConfigTests
    {
        [Test]
        [ExpectedException(typeof(BootstrapConfigurationException))]
        public void When_Pools_Element_Is_Empty_BootstrapConfigurationException_Is_Thrown()
        {
            var serializer = new JavaScriptSerializer();
            serializer.RegisterConverters(new List<JavaScriptConverter>{ClusterNode.BootstrapConfigConverterInstance});

            var config = File.ReadAllText(@"Data\\bootstrap-with-empty-pools.json");
            var info = serializer.Deserialize<BootstrapInfo>(config);
        }

        [Test]
        public void Test_That_Config_Can_Deserialize()
        {
            var serializer = new JavaScriptSerializer();
            serializer.RegisterConverters(new List<JavaScriptConverter> { ClusterNode.BootstrapConfigConverterInstance });

            var config = File.ReadAllText(@"Data\\bootstrap.json");
            var info = serializer.Deserialize<BootstrapInfo>(config);

            Assert.IsNotNull(info);
            Assert.AreEqual("default", info.Name);
            Assert.AreEqual("/pools/default?uuid=23029c48891e8a897fe3c5c0eade5021", info.Uri);
            Assert.AreEqual("/poolsStreaming/default?uuid=23029c48891e8a897fe3c5c0eade5021", info.StreamingUri);
        }
    }
}
