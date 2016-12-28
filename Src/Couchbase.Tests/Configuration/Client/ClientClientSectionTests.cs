using System;
using System.Configuration;
using System.Linq;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Client.Providers;
using Couchbase.IO;
using NUnit.Framework;

namespace Couchbase.Tests.Configuration.Client
{
    [TestFixture]
    public class ClientClientSectionTests
    {
        [Test]
        public void When_GetSection_Called_Section_Is_QueryRequestTimeout_Defaults_To_75_Seconds()
        {
            var section =  (CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase");
            Assert.AreEqual(75000, section.QueryRequestTimeout);
        }

        [Test]
        public void When_GetSection_Called_Section_Is_Set_QueryRequestTimeout_Returns_Value()
        {
            var section = (CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase_1");
            Assert.AreEqual(10000, section.QueryRequestTimeout);
        }

        [Test]
        public void When_GetSection_Called_Section_Is_Returned()
        {
            var section = ConfigurationManager.GetSection("couchbaseClients/couchbase");
            Assert.IsNotNull(section);
        }

        [Test]
        public void When_GetSection_Called_CouchbaseClientSection_Is_Returned()
        {
            var section = ConfigurationManager.GetSection("couchbaseClients/couchbase");
            Assert.IsNotNull(section as CouchbaseClientSection);
        }

        [Test]
        public void Test_That_CouchbaseClientSection_Has_ServerIp_Uri()
        {
            var section = (CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase");
            var servers = new UriElement[section.Servers.Count];
// ReSharper disable once CoVariantArrayConversion
            section.Servers.CopyTo(servers, 0);
            Assert.That(() => !string.IsNullOrEmpty(servers[0].Uri.OriginalString));
        }

        [Test]
        public void Test_That_CouchbaseClientSection_Has_At_Least_One_Element()
        {
            var section = (CouchbaseClientSection) ConfigurationManager.GetSection("couchbaseClients/couchbase");
            Assert.Greater(section.Servers.Count, 0);
        }

        [Test]
        public void When_UseSsl_Is_True_In_AppConfig_UseSsl_Returns_True()
        {
            var section = (CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase_1");
            Assert.IsTrue(section.UseSsl);
        }

        [Test]
        public void When_No_Bucket_Is_Defined_Default_Bucket_Is_Used()
        {
            var section = (CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase");
            Assert.Greater(section.Buckets.Count, 0);

            var buckets = new BucketElement[section.Buckets.Count];
            section.Buckets.CopyTo(buckets, 0);
            Assert.AreEqual("default", buckets[0].Name);
        }

        [Test]
        public void When_Bucket_Is_Defined_That_Bucket_Is_Used()
        {
            var section = (CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase_1");
            Assert.Greater(section.Buckets.Count, 0);

            var buckets = new BucketElement[section.Buckets.Count];
            section.Buckets.CopyTo(buckets, 0);

            var bucket = buckets.First();
            Assert.AreEqual("testbucket", bucket.Name);
            Assert.AreEqual("shhh!", bucket.Password);
            Assert.IsFalse(bucket.UseSsl);
        }

        [Test]
        public void When_Bucket_Contains_ConnectionPoolElement_It_Is_Used()
        {
            var section = (CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase_1");
            var buckets = new BucketElement[section.Buckets.Count];
            section.Buckets.CopyTo(buckets, 0);

            var bucket = buckets.First();

            Assert.IsNotNull(bucket.ConnectionPool);
            Assert.AreEqual(5000, bucket.ConnectionPool.WaitTimeout);
            Assert.AreEqual(3000, bucket.ConnectionPool.ShutdownTimeout);
            Assert.AreEqual(12000, bucket.ConnectionPool.SendTimeout);
            Assert.AreEqual(10, bucket.ConnectionPool.MaxSize);
            Assert.AreEqual(5, bucket.ConnectionPool.MinSize);
            Assert.AreEqual("custom", bucket.ConnectionPool.Name);
        }

        [Test]
        public void When_Bucket_Does_Not_Contain_ConnectionPoolElement_Default_Is_Used()
        {
            var section = (CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase");
            var buckets = new BucketElement[section.Buckets.Count];
            section.Buckets.CopyTo(buckets, 0);

            var bucket = buckets.First();

            Assert.IsNotNull(bucket.ConnectionPool);
            Assert.AreEqual(2500, bucket.ConnectionPool.WaitTimeout);
            Assert.AreEqual(10000, bucket.ConnectionPool.ShutdownTimeout);
            Assert.AreEqual(2, bucket.ConnectionPool.MaxSize);
            Assert.AreEqual(1, bucket.ConnectionPool.MinSize);
            Assert.AreEqual("default", bucket.ConnectionPool.Name);
        }

        [Test]
        public void Test_Default_Ports()
        {
            var section = (CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase");
            Assert.AreEqual(11207, section.SslPort);
            Assert.AreEqual(8091, section.MgmtPort);
            Assert.AreEqual(8092, section.ApiPort);
            Assert.AreEqual(18091, section.HttpsMgmtPort);
            Assert.AreEqual(18092, section.HttpsApiPort);
            Assert.AreEqual(11210, section.DirectPort);
        }

        [Test]
        public void Test_Custom_Ports()
        {
            var section = (CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase_1");
            Assert.AreEqual(443, section.SslPort);
            Assert.AreEqual(8095, section.MgmtPort);
            Assert.AreEqual(8094, section.ApiPort);
            Assert.AreEqual(18099, section.HttpsMgmtPort);
            Assert.AreEqual(18098, section.HttpsApiPort);
            Assert.AreEqual(11219, section.DirectPort);
        }

        [Test]
        public void Test_EnableTcpKeepAlives()
        {
            var section = (CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase_3");
            Assert.AreEqual(true, section.EnableTcpKeepAlives);
            Assert.AreEqual(100, section.TcpKeepAliveInterval);
            Assert.AreEqual(10, section.TcpKeepAliveTime);

            var configuration = new ClientConfiguration(section);
            configuration.Initialize();

            var bucket = configuration.BucketConfigs["default"];
            Assert.AreEqual(true, bucket.PoolConfiguration.EnableTcpKeepAlives);
            Assert.AreEqual(100, bucket.PoolConfiguration.TcpKeepAliveInterval);
            Assert.AreEqual(10, bucket.PoolConfiguration.TcpKeepAliveTime);
        }

        [Test]
        public void Test_Programmatic_Config_Construction_Using_Default_Settings()
        {
            var cluster = new Cluster("couchbaseClients/couchbase");
            var serverIp = ConfigurationManager.AppSettings["serverIp"];
            var configuration = cluster.Configuration;
            Assert.AreEqual(11207, configuration.SslPort);
            Assert.AreEqual(8091, configuration.MgmtPort);
            Assert.AreEqual(8092, configuration.ApiPort);
            Assert.AreEqual(18091, configuration.HttpsMgmtPort);
            Assert.AreEqual(18092, configuration.HttpsApiPort);
            Assert.AreEqual(11210, configuration.DirectPort);
            Assert.IsFalse(configuration.UseSsl);
            Assert.AreEqual(75000, configuration.ViewRequestTimeout);

            var server = configuration.Servers.First();
            Assert.AreEqual(new Uri(string.Format("http://{0}:8091/pools", serverIp)).PathAndQuery, server.PathAndQuery);

            var bucketKvp = configuration.BucketConfigs.First();
            Assert.AreEqual("default", bucketKvp.Key);
            Assert.AreEqual(string.Empty, bucketKvp.Value.Password);
            Assert.IsFalse(bucketKvp.Value.UseSsl);
            Assert.AreEqual("default", bucketKvp.Value.BucketName);

            var poolConfiguration = bucketKvp.Value.PoolConfiguration;
            Assert.IsFalse(poolConfiguration.UseSsl);
            Assert.AreEqual(2, poolConfiguration.MaxSize);
            Assert.AreEqual(1, poolConfiguration.MinSize);
            Assert.AreEqual(2500, poolConfiguration.WaitTimeout);
            Assert.AreEqual(10000, poolConfiguration.ShutdownTimeout);
        }

        [Test]
        public void Test_Programmatic_Config_Construction_Using_Custom_Settings()
        {
            using (var cluster = new Cluster("couchbaseClients/couchbase_1"))
            {
                var configuration = cluster.Configuration;
                Assert.AreEqual(443, configuration.SslPort);
                Assert.AreEqual(8095, configuration.MgmtPort);
                Assert.AreEqual(8094, configuration.ApiPort);
                Assert.AreEqual(18099, configuration.HttpsMgmtPort);
                Assert.AreEqual(18098, configuration.HttpsApiPort);
                Assert.AreEqual(11219, configuration.DirectPort);
                Assert.IsTrue(configuration.UseSsl);
                Assert.AreEqual(15, configuration.QueryFailedThreshold);
                Assert.AreEqual(5000, configuration.ViewRequestTimeout);

                var server = configuration.Servers.First();
                Assert.AreEqual(new Uri("https://localhost2:18099/pools"), server);

                var bucketKvp = configuration.BucketConfigs.First();
                Assert.AreEqual("testbucket", bucketKvp.Key);
                Assert.AreEqual("shhh!", bucketKvp.Value.Password);
                Assert.IsTrue(bucketKvp.Value.UseSsl);
                Assert.AreEqual("testbucket", bucketKvp.Value.BucketName);
                Assert.AreEqual(2, configuration.BucketConfigs.Count);

                var poolConfiguration = bucketKvp.Value.PoolConfiguration;
                Assert.IsTrue(poolConfiguration.UseSsl);
                Assert.AreEqual(10, poolConfiguration.MaxSize);
                Assert.AreEqual(5, poolConfiguration.MinSize);
                Assert.AreEqual(5000, poolConfiguration.WaitTimeout);
                Assert.AreEqual(3000, poolConfiguration.ShutdownTimeout);
                Assert.AreEqual(12000, poolConfiguration.SendTimeout);
            }
        }

        [Test]
        public void When_Bucket_UseSsl_Is_True_In_AppConfig_UseSsl_Returns_True()
        {
            var section = (CouchbaseClientSection) ConfigurationManager.GetSection("couchbaseClients/couchbase_2");

            var buckets = new BucketElement[section.Buckets.Count];
            section.Buckets.CopyTo(buckets, 0);

            Assert.IsFalse(section.UseSsl);
            Assert.IsTrue(buckets.First().UseSsl);
        }

        [Test]
        public void When_MaxCloseAttempts_And_MaxCloseInterval_Changed_In_AppConfig_ConnectionPoolElement_Is_Updated()
        {
            var section = (CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase_4");
            var buckets = new BucketElement[section.Buckets.Count];
            section.Buckets.CopyTo(buckets, 0);

            var default2 = buckets[1];
            Assert.AreEqual(6, default2.ConnectionPool.MaxCloseAttempts);
            Assert.AreEqual(120, default2.ConnectionPool.CloseAttemptInterval);
        }

        [Test]
        public void When_MaxCloseAttempts_And_MaxCloseInterval_Changed_In_AppConfig_ClientConfiguration_Is_Updated()
        {
            var section = (CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase_4");
            var config = new ClientConfiguration(section);
            var default2 = config.BucketConfigs["default2"];

            Assert.AreEqual(6, default2.PoolConfiguration.MaxCloseAttempts);
            Assert.AreEqual(120, default2.PoolConfiguration.CloseAttemptInterval);
        }

        [Test]
        public void When_Initialize_Called_With_AppConfig_Settings_Bucket_Can_Be_Opened()
        {
            using (var cluster = new Cluster("couchbaseClients/couchbase"))
            {
                var bucket = cluster.OpenBucket();
                Assert.AreEqual("default", bucket.Name);

                var result = bucket.Upsert("testkey", "testvalue");
                Assert.IsTrue(result.Success);
                Assert.AreEqual(ResponseStatus.Success, result.Status);

                var result2 = bucket.Get<string>("testkey");
                Assert.IsTrue(result2.Success);
                Assert.AreEqual(ResponseStatus.Success, result2.Status);
                Assert.AreEqual("testvalue", result2.Value);
            }
        }

        [Test]
        public void Test_Connection_Pool_Settings_Inheritance()
        {
            //use section with client-wide connection pool settings and one bucket overriding the settings
            var section = (CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase_5");
            var config = new ClientConfiguration(section);
            config.Initialize();

            Assert.AreEqual(4, config.BucketConfigs["default"].PoolConfiguration.MaxSize, "Bucket specific setting of MaxSize=4 should have been used.");
            Assert.AreEqual(3, config.BucketConfigs["default2"].PoolConfiguration.MaxSize, "Client default setting of MaxSize=3 should have been used.");

            //use section with no connection pool definitions at all
            section = (CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase_2");
            config = new ClientConfiguration(section);
            config.Initialize();

            //get default value for connection pool max size
            var propInfo = typeof(ConnectionPoolElement).GetProperty("MaxSize");
            var confProp = (ConfigurationPropertyAttribute)Attribute.GetCustomAttribute(propInfo, typeof(ConfigurationPropertyAttribute));

            Assert.AreEqual(confProp.DefaultValue, config.BucketConfigs["default"].PoolConfiguration.MaxSize, "Default setting of MaxSize should have been used.");
        }
    }
}

#region [ License information ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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
