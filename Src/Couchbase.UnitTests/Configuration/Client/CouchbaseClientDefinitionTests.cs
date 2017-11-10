using System;
using System.Collections.Generic;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Providers;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace Couchbase.UnitTests.Configuration.Client
{
    [TestFixture]
    public class CouchbaseClientDefinitionTests
    {
        [Test]
        [TestCase("HttpStreaming", ServerConfigurationProviders.HttpStreaming)]
        [TestCase("CarrierPublication", ServerConfigurationProviders.CarrierPublication)]
        [TestCase("CarrierPublication,HttpStreaming", ServerConfigurationProviders.CarrierPublication | ServerConfigurationProviders.HttpStreaming)]
        public void ConfigurationProviders_ParsedCorrectly(string value, ServerConfigurationProviders expected)
        {
            var builder = new ConfigurationBuilder();
            builder.AddInMemoryCollection(new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("Couchbase:ConfigurationProviders", value)
            });

            var config = builder.Build();

            var definition = new CouchbaseClientDefinition();

            config.GetSection("Couchbase").Bind(definition);

            Assert.AreEqual(expected, definition.ConfigurationProviders);
        }
    }
}
