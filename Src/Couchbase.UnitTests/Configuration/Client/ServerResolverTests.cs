using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NUnit.Framework;
using Couchbase.Configuration.Client;

#if NET45
using System.Configuration;
using Couchbase.Configuration.Client.Providers;
#endif

namespace Couchbase.UnitTests.Configuration.Client
{
    [TestFixture]
    public class ServerResolverTests
    {
        [Test]
        public void Uses_Default_Server_If_No_ServerResolver_Type_Set()
        {
            var section = new Mock<ICouchbaseClientDefinition>();
            section.Setup(x => x.ServerResolverType).Returns((string)null);

            var client = new ClientConfiguration(section.Object);

            Assert.IsNotNull(client.Servers);
            Assert.AreEqual(client.Servers.Count, 1);
            Assert.AreEqual(ClientConfiguration.Defaults.Server, client.Servers.First());
        }

        [Test]
        public void Throws_Exception_If_Unable_To_Resolve_Type()
        {
            const string resolverType = "Somehere.UnknownResolver, Somwhere";
            var section = new Mock<ICouchbaseClientDefinition>();
            section.Setup(x => x.ServerResolverType).Returns(resolverType);

            Assert.Throws<Exception>(() => new ClientConfiguration(section.Object), "Unable to find or build type '{0}' for use as a IServerResolver.", resolverType);
        }

        [Test]
        public void Throws_Exception_If_Type_Doesnt_Conform_To_Interface()
        {
            var resolverType = GetResolverType("InvalidServerResolver");

            var section = new Mock<ICouchbaseClientDefinition>();
            section.Setup(x => x.ServerResolverType).Returns(resolverType);

            Assert.Throws<Exception>(() => new ClientConfiguration(section.Object),
                "Unable to use type '{0}' as a server resolver because it does not conform to interface '{1}'.",
                resolverType, typeof(IServerResolver));
        }

        [Test]
        public void Throws_Exception_If_Resolver_Throws_Exception()
        {
            var resolverType = GetResolverType("ExceptionServerResolver");

            var section = new Mock<ICouchbaseClientDefinition>();
            section.Setup(x => x.ServerResolverType).Returns(resolverType);

            Assert.Throws<Exception>(() => new ClientConfiguration(section.Object), "Something went wrong.");
        }

        [Test]
        public void Throws_Exception_If_No_Uris_Are_Returned_From_Resolver()
        {
            var resolverType = GetResolverType("EmptyServerResolver");

            var section = new Mock<ICouchbaseClientDefinition>();
            section.Setup(x => x.ServerResolverType).Returns(resolverType);

            Assert.Throws<Exception>(() => new ClientConfiguration(section.Object), "Did not find any servers using resolver '{0}'.", resolverType);
        }

        [Test]
        public void Uses_Resolver_Uris_If_Available()
        {
            var resolverType = GetResolverType("TestServerResolver");

            var section = new Mock<ICouchbaseClientDefinition>();
            section.Setup(x => x.ServerResolverType).Returns(resolverType);

            var client = new ClientConfiguration(section.Object);

            Assert.IsNotNull(client.Servers);
            Assert.AreEqual(client.Servers.Count, 2);
            Assert.AreEqual("http://node1.example.com:8091", client.Servers[0].OriginalString);
            Assert.AreEqual("http://node2.example.com:8091", client.Servers[1].OriginalString);
        }

        [Test]
        public void Prefers_ServerResolver_Over_Config_Servers()
        {
            var resolverType = GetResolverType("TestServerResolver");

            var section = new Mock<ICouchbaseClientDefinition>();
            section.Setup(x => x.ServerResolverType).Returns(resolverType);
            section.Setup(x => x.Servers).Returns(new List<Uri> { new Uri("http://node3.example.com") });

            var client = new ClientConfiguration(section.Object);

            Assert.IsNotNull(client.Servers);
            Assert.AreEqual(client.Servers.Count, 2);
            Assert.AreEqual("http://node1.example.com:8091", client.Servers[0].OriginalString);
            Assert.AreEqual("http://node2.example.com:8091", client.Servers[1].OriginalString);
        }

#if NET45
        [Test]
        public void Can_Load_ResolverType_From_Config()
        {
            var client = new ClientConfiguration((CouchbaseClientSection) ConfigurationManager.GetSection("couchbaseClients/couchbase_dns"));

            Assert.IsNotNull(client.Servers);
            Assert.AreEqual(client.Servers.Count, 2);
            Assert.AreEqual("http://node1.example.com:8091", client.Servers[0].OriginalString);
            Assert.AreEqual("http://node2.example.com:8091", client.Servers[1].OriginalString);
        }
#endif

        private string GetResolverType(string resolverName)
        {
            return string.Format("Couchbase.UnitTests.Configuration.Client.{0}, Couchbase.UnitTests", resolverName);
        }
    }

    public class InvalidServerResolver
    {

    }

    public class ExceptionServerResolver : IServerResolver
    {
        public List<Uri> GetServers()
        {
            throw new Exception("Something went wrong.");
        }
    }

    public class EmptyServerResolver : IServerResolver
    {
        public List<Uri> GetServers()
        {
            return new List<Uri>();
        }
    }

    public class TestServerResolver : IServerResolver
    {
        public List<Uri> GetServers()
        {
            return new List<Uri>
            {
                new Uri("http://node1.example.com:8091"),
                new Uri("http://node2.example.com:8091")
            };
        }
    }
}