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

        /// <summary>
        /// @test: create couchbase client using configuration and http client is not set, 
        /// then it creates instance of RestSharp http client
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if result is of type RestSharp http client
        /// </summary>
		[Test]
		public void When_Using_Code_Config_And_Http_Client_Factory_Is_Not_Set_RestSharp_Factory_Is_Default()
		{
			var config = new CouchbaseClientConfiguration();
			config.Urls.Add(new Uri("http://localhost:8091/pools"));
			Assert.That(config.HttpClientFactory, Is.InstanceOf<RestSharpHttpClientFactory>());

			//RestSharpHttpClient is an internal class to the Couchbase assembly,
			//therefore the explicit type can't be checked for using Is.InstanceOf<T>
			var typeName = (config.HttpClientFactory.Create(config.Urls[0], "", "", TimeSpan.FromMinutes(1), true).GetType().Name);
			Assert.That(typeName, Is.StringContaining("RestSharpHttpClient"));
		}

        /// <summary>
        /// @test: create couchbase client using configuration from app.config and http client is not set, 
        /// then it creates instance of RestSharp http client
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if result is of type RestSharp http client
        /// </summary>
		[Test]
		public void When_Using_App_Config_And_Http_Client_Factory_Is_Not_Set_RestSharp_Factory_Is_Default()
		{
			var config = ConfigurationManager.GetSection("min-config") as CouchbaseClientSection;

			Assert.That(config, Is.Not.Null, "min-config section missing from app.config");
			Assert.That(config.HttpClientFactory, Is.InstanceOf<ProviderElement<IHttpClientFactory>>());

			//RestSharpHttpClient is an internal class to the Couchbase assembly,
			//therefore the explicit type can't be checked for using Is.InstanceOf<T>
			var typeName = (config.HttpClientFactory.CreateInstance().Create(config.Servers.Urls.ToUriCollection()[0], "", "", TimeSpan.FromMinutes(1), true).GetType().Name);
			Assert.That(typeName, Is.StringContaining("RestSharpHttpClient"));
		}

        /// <summary>
        /// @test: create couchbase client using configuration and http client is not set, 
        /// then it creates instance of RestSharp http client. perform operations like storing key value, 
        /// the operations should all succeed
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if all operations succeed
        /// </summary>
		[Test]
		public void When_Using_App_Config_And_Http_Client_Factory_Is_Not_Set_Operations_Succeed()
		{
			var config = ConfigurationManager.GetSection("min-config") as CouchbaseClientSection;

			Assert.That(config, Is.Not.Null, "min-config section missing from app.config");
			Assert.That(config.HttpClientFactory, Is.InstanceOf<ProviderElement<IHttpClientFactory>>());

			var client = new CouchbaseClient(config);
			var kv = KeyValueUtils.GenerateKeyAndValue("default_config");

			var result = client.ExecuteStore(StoreMode.Add, kv.Item1, kv.Item2);
			Assert.That(result.Success, Is.True, "Store failed: " + result.Message);

			var value = client.Get(kv.Item1);
			Assert.That(value, Is.StringMatching(kv.Item2));
		}

        /// <summary>
        /// @test: create couchbase client using configuration from code and http client is not set, 
        /// then it creates instance of RestSharp http client. perform operations like
        /// get and store and they should all pass
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if all operations should happen successfully
        /// </summary>
		[Test]
		public void When_Using_Code_Config_And_Http_Client_Factory_Is_Not_Set_Operations_Succeed()
		{
			var config = new CouchbaseClientConfiguration();
			config.Urls.Add(new Uri("http://localhost:8091/pools"));
			Assert.That(config.HttpClientFactory, Is.InstanceOf<RestSharpHttpClientFactory>());

			Assert.That(config, Is.Not.Null, "min-config section missing from app.config");
			Assert.That(config.HttpClientFactory, Is.InstanceOf<RestSharpHttpClientFactory>());

			var client = new CouchbaseClient(config);
			var kv = KeyValueUtils.GenerateKeyAndValue("default_config");

			var result = client.ExecuteStore(StoreMode.Add, kv.Item1, kv.Item2);
			Assert.That(result.Success, Is.True, "Store failed: " + result.Message);

			var value = client.Get(kv.Item1);
			Assert.That(value, Is.StringMatching(kv.Item2));
		}

		#endregion

		#region Design Doc Name Transformer Tests

        /// <summary>
        /// @test: Create couchbase client using code configuration, create design document with
        /// no transformer name, the default mode will be production
        /// @pre: Provide configuration of client like Uri, etc
        /// @post: Test passes if Production mode is the default mode
        /// </summary>
		[Test]
		public void When_Using_Code_Config_And_Design_Document_Name_Transformer_Is_Not_Set_Production_Mode_Is_Default()
		{
			var config = new CouchbaseClientConfiguration();
			config.Urls.Add(new Uri(ConfigurationManager.AppSettings["CouchbaseServerUrl"] + "/pools"));
			var client = new CouchbaseClient(config); //client sets up transformer

			Assert.That(config.DesignDocumentNameTransformer, Is.InstanceOf<ProductionModeNameTransformer>());		}

        /// <summary>
        /// @test: Create couchbase client using configuration from App.config, create design document with
        /// no transformer name, the default mode will be production
        /// @pre: Provide configuration of client in app.config
        /// @post: Test passes if Production mode is the default mode
        /// </summary>
		[Test]
		public void When_Using_App_Config_And_Design_Document_Name_Transformer_Is_Not_Set_Production_Mode_Is_Default()
		{
			var config = ConfigurationManager.GetSection("min-config") as CouchbaseClientSection;
			var client = new CouchbaseClient(config); //client sets up transformer

			Assert.That(config.DocumentNameTransformer.Type.Name, Is.StringMatching("ProductionModeNameTransformer"));

		}

		#endregion

		#region Timeouts

        /// <summary>
        /// @test: Create couchbase client and dont set time out, default would be 20 seconds
        /// @pre: Provide configuration of client in app.config
        /// @post: Test passes if http request time out is 20 seconds
        /// </summary>
		[Test]
		public void When_Http_Timeout_Is_Not_Set_And_Using_App_Config_Default_Is_20_Seconds()
		{
			var config = ConfigurationManager.GetSection("httptimeout-default-config") as CouchbaseClientSection;
			Assert.That(config, Is.Not.Null, "Config was null");
			Assert.That(config.Servers.HttpRequestTimeout, Is.EqualTo(TimeSpan.FromSeconds(20)));
		}

        /// <summary>
        /// @test: Create couchbase client using code configuration and dont set time out, default would be 20 seconds
        /// @pre: Provide configuration of client in code
        /// @post: Test passes if http request time out is 20 seconds
        /// </summary>
		[Test]
		public void When_Http_Timeout_Is_Not_Set_And_Using_Code_Config_Default_Is_20_Seconds()
		{
			var config = new CouchbaseClientConfiguration();
			Assert.That(config, Is.Not.Null, "Config was null");
			Assert.That(config.HttpRequestTimeout, Is.EqualTo(TimeSpan.FromSeconds(20)));
		}

        /// <summary>
        /// @test: Create couchbase client, set time out to 30 seconds, server time out should be 30 seconds,
        /// it will overwrite the default time out
        /// @pre: Provide configuration of client in app.config
        /// @post: Test passes if http request time out is 30 seconds
        /// </summary>
		[Test]
		public void When_Http_Timeout_Is_Set_To_30_And_Using_App_Config_Value_Is_30_Seconds()
		{
			var config = ConfigurationManager.GetSection("httptimeout-explicit-config") as CouchbaseClientSection;
			Assert.That(config, Is.Not.Null, "Config was null");
			Assert.That(config.Servers.HttpRequestTimeout, Is.EqualTo(TimeSpan.FromSeconds(30)));
		}

        /// <summary>
        /// @test: Create couchbase client using configuration from app.config and dont set observe time out,
        /// default would be 1 minute
        /// @pre: Provide configuration of client in app.config
        /// @post: Test passes if observe request time out is 1 minute
        /// </summary>
		[Test]
		public void When_Observe_Timeout_Is_Not_Set_And_Using_App_Config_Default_Is_1_Minute()
		{
			var config = ConfigurationManager.GetSection("observe-default-config") as CouchbaseClientSection;
			Assert.That(config, Is.Not.Null, "Config was null");
			Assert.That(config.Servers.ObserveTimeout, Is.EqualTo(TimeSpan.FromMinutes(1)));
		}

        /// <summary>
        /// @test: Create couchbase client using code config and dont set observe time out, default would be 1 minute
        /// @pre: Provide configuration of client in code config
        /// @post: Test passes if observe request time out is 1 minute
        /// </summary>
		[Test]
		public void When_Observe_Timeout_Is_Not_Set_And_Using_Code_Config_Default_Is_1_Minute()
		{
			var config = new CouchbaseClientConfiguration();
			Assert.That(config, Is.Not.Null, "Config was null");
			Assert.That(config.ObserveTimeout, Is.EqualTo(TimeSpan.FromMinutes(1)));
		}

        /// <summary>
        /// @test: Create couchbase client usng ap.config and set observe time out to 30 seconds,
        /// the time out would be 30 seconds
        /// @pre: Provide configuration of client in app.config
        /// @post: Test passes if observe request time out is 30 seconds
        /// </summary>
		[Test]
		public void When_Observe_Timeout_Is_Set_To_30_And_Using_App_Config_Value_Is_30_Seconds()
		{
			var config = ConfigurationManager.GetSection("observe-explicit-config") as CouchbaseClientSection;
			Assert.That(config, Is.Not.Null, "Config was null");
			Assert.That(config.Servers.ObserveTimeout, Is.EqualTo(TimeSpan.FromSeconds(30)));
		}
		#endregion

		#region HttpClient

        /// <summary>
        /// @test: Default value of InitializeConnection property in HttpClient class from appconfig is true.
        /// @pre: Provide configuration of client in app.config, dont specify InitializeConnection
        /// @post: Test passes if HttpClient.InitializeConnection returns true
        /// </summary>
		[Test]
		public void When_Initialize_Connection_Is_Not_Set_In_App_Config_Default_Is_True()
		{
			var config = ConfigurationManager.GetSection("min-config") as CouchbaseClientSection;
			Assert.That(config, Is.Not.Null, "Config was null");
            Assert.That(config.HttpClient.InitializeConnection, Is.True);
		}

        /// <summary>
        /// @test: When InitializeConnection property in HttpClient class is set in app.config,
        /// default value is overwritten.
        /// @pre: Provide configuration of client in app.config, set InitializeConnection to false
        /// @post: Test passes if HttpClient.InitializeConnection returns false
        /// </summary>
		[Test]
		public void When_Initialize_Connection_Is_Set_In_App_Config_Property_Changes_From_Default()
		{
			var config = ConfigurationManager.GetSection("httpclient-config-noinitconn") as CouchbaseClientSection;
			Assert.That(config, Is.Not.Null, "Config was null");
			Assert.That(config.HttpClient.InitializeConnection, Is.False);
		}

        /// <summary>
        /// @test: Default value of InitializeConnection property in HttpClient class from code config is true.
        /// @pre: Provide configuration of client in app.config, dont specify InitializeConnection
        /// @post: Test passes if HttpClient.InitializeConnection returns true
        /// </summary>
		[Test]
		public void When_Initialize_Connection_Is_Not_Set_In_Code_Default_Is_True()
		{
			var config = new CouchbaseClientConfiguration();
			Assert.That(config, Is.Not.Null, "Config was null");
			Assert.That(config.HttpClient.InitializeConnection, Is.True);
		}

        /// <summary>
        /// @test: Default value of Httpclient time out property in HttpClient class from appconfig is 00:01:15.
        /// @pre: Provide configuration of client in app.config, dont specify timeout
        /// @post: Test passes if HttpClient.timeout is 00:01:15
        /// </summary>
        [Test]
		public void When_Http_Client_Timeout_Is_Not_Set_In_App_Config_Default_Is_True()
		{
			var config = ConfigurationManager.GetSection("min-config") as CouchbaseClientSection;
			Assert.That(config, Is.Not.Null, "Config was null");
			Assert.That(config.HttpClient.Timeout, Is.EqualTo(TimeSpan.Parse("00:01:15")));
		}

        /// <summary>
        /// @test: Default value of Httpclient time out property in HttpClient class from appconfig
        /// is overwritten if specified
        /// @pre: Provide configuration of client in app.config, dont specify timeout
        /// @post: Test passes if HttpClient.timeout isas per mentioned in app.config
        /// </summary>
		[Test]
		public void When_Http_Client_Timeout_Is_Set_In_App_Config_Property_Changes_From_Default()
		{
			var config = ConfigurationManager.GetSection("httpclient-config-noinitconn") as CouchbaseClientSection;
			Assert.That(config, Is.Not.Null, "Config was null");
			Assert.That(config.HttpClient.Timeout, Is.EqualTo(TimeSpan.Parse("00:00:45")));
		}

        /// <summary>
        /// @test: Default value of Httpclient time out property in HttpClient class is 75 seconds
        /// @pre: Provide default configuration of client
        /// @post: Test passes if HttpClient.timeout is 00:01:15
        /// </summary>
		[Test]
		public void When_Http_Client_Timeout_Is_Not_Set_In_Code_Default_Is_75_Seconds()
		{
			var config = new CouchbaseClientConfiguration();
			Assert.That(config, Is.Not.Null, "Config was null");
			Assert.That(config.HttpClient.Timeout, Is.EqualTo(TimeSpan.Parse("00:01:15")));
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