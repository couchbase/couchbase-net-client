using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Couchbase.Configuration;
using System.Configuration;
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached;
using Couchbase.Tests.Utils;

namespace Couchbase.Tests
{
	[TestFixture]
	public class DefaultConfigurationSettingsTests
	{
		#region HTTP Factory Tests

		[Test]
		public void When_Using_Code_Config_And_Http_Client_Factory_Is_Not_Set_Hammock_Factory_Is_Default()
		{
			var config = new CouchbaseClientConfiguration();
			config.Urls.Add(new Uri("http://localhost:8091/pools"));
			Assert.That(config.HttpClientFactory, Is.InstanceOf<HammockHttpClientFactory>());

			//HammockHttpClient is an internal class to the Couchbase assembly,
			//therefore the explicit type can't be checked for using Is.InstanceOf<T>
			var typeName = (config.HttpClientFactory.Create(config.Urls[0], "", "").GetType().Name);
			Assert.That(typeName, Is.StringContaining("HammockHttpClient"));
		}

		[Test]
		public void When_Using_App_Config_And_Http_Client_Factory_Is_Not_Set_Hammock_Factory_Is_Default()
		{
			var config = ConfigurationManager.GetSection("min-config") as CouchbaseClientSection;

			Assert.That(config, Is.Not.Null, "min-config section missing from app.config");
			Assert.That(config.HttpClientFactory, Is.InstanceOf<ProviderElement<IHttpClientFactory>>());

			//HammockHttpClient is an internal class to the Couchbase assembly,
			//therefore the explicit type can't be checked for using Is.InstanceOf<T>
			var typeName = (config.HttpClientFactory.CreateInstance().Create(config.Servers.Urls.ToUriCollection()[0], "", "").GetType().Name);
			Assert.That(typeName, Is.StringContaining("HammockHttpClient"));
		}

		[Test]
		public void When_Using_App_Config_And_Http_Client_Factory_Is_Not_Set_Operations_Succeed()
		{
			var config = ConfigurationManager.GetSection("min-config") as CouchbaseClientSection;

			Assert.That(config, Is.Not.Null, "min-config section missing from app.config");
			Assert.That(config.HttpClientFactory, Is.InstanceOf<ProviderElement<IHttpClientFactory>>());

			var client = new CouchbaseClient(config);
			var kv = KeyValueUtils.GenerateKeyAndValue("default_config");

			var result = client.Store(StoreMode.Add, kv.Item1, kv.Item2);
			Assert.That(result, Is.True, "Store failed");

			var value = client.Get(kv.Item1);
			Assert.That(value, Is.StringMatching(kv.Item2));
		}

		[Test]
		public void When_Using_Code_Config_And_Http_Client_Factory_Is_Not_Set_Operations_Succeed()
		{
			var config = new CouchbaseClientConfiguration();
			config.Urls.Add(new Uri("http://localhost:8091/pools"));
			Assert.That(config.HttpClientFactory, Is.InstanceOf<HammockHttpClientFactory>());

			Assert.That(config, Is.Not.Null, "min-config section missing from app.config");
			Assert.That(config.HttpClientFactory, Is.InstanceOf<HammockHttpClientFactory>());

			var client = new CouchbaseClient(config);
			var kv = KeyValueUtils.GenerateKeyAndValue("default_config");

			var result = client.Store(StoreMode.Add, kv.Item1, kv.Item2);
			Assert.That(result, Is.True, "Store failed");

			var value = client.Get(kv.Item1);
			Assert.That(value, Is.StringMatching(kv.Item2));
		}

		#endregion

		#region Design Doc Name Transformer Tests

		[Test]
		public void When_Using_Code_Config_And_Design_Document_Name_Transformer_Is_Not_Set_Production_Mode_Is_Default()
		{
			var config = new CouchbaseClientConfiguration();
			config.Urls.Add(new Uri("http://localhost:8091/pools"));
			var client = new CouchbaseClient(config); //client sets up transformer

			Assert.That(config.DesignDocumentNameTransformer, Is.InstanceOf<ProductionModeNameTransformer>());		}

		[Test]
		public void When_Using_App_Config_And_Design_Document_Name_Transformer_Is_Not_Set_Production_Mode_Is_Default()
		{
			var config = ConfigurationManager.GetSection("min-config") as CouchbaseClientSection;
			var client = new CouchbaseClient(config); //client sets up transformer

			Assert.That(config.DocumentNameTransformer.Type.Name, Is.StringMatching("ProductionModeNameTransformer"));

		}

		#endregion
	}
}

#region [ License information          ]
/* ************************************************************
 * 
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2012 Couchbase, Inc.
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