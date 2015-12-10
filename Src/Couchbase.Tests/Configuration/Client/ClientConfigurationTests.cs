using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Client.Providers;
using Couchbase.Tests.Fakes;
using Couchbase.Tests.Utils;
using NUnit.Framework;

namespace Couchbase.Tests.Configuration.Client
{
    [TestFixture]
    public class ClientConfigurationTests
    {
        private string _ipAddress = ConfigurationManager.AppSettings["serverIp"];

        [Test]
        public void Test_Default()
        {
            var config = new ClientConfiguration();
            Assert.AreEqual(1, config.BucketConfigs.Count);

            var bucketConfig = config.BucketConfigs.First().Value;

            IPAddress ipAddress;
            IPAddress.TryParse("127.0.0.1", out ipAddress);
            var endPoint = new IPEndPoint(ipAddress, bucketConfig.Port);
            Assert.AreEqual(endPoint, bucketConfig.GetEndPoint());

            Assert.IsEmpty(bucketConfig.Password);
            Assert.IsEmpty(bucketConfig.Username);
            Assert.AreEqual(11210, bucketConfig.Port);
            Assert.AreEqual("default", bucketConfig.BucketName);

            Assert.AreEqual(2, bucketConfig.PoolConfiguration.MaxSize);
            Assert.AreEqual(1, bucketConfig.PoolConfiguration.MinSize);
            Assert.AreEqual(2500, bucketConfig.PoolConfiguration.RecieveTimeout);
            Assert.AreEqual(2500, bucketConfig.PoolConfiguration.OperationTimeout);
            Assert.AreEqual(10000, bucketConfig.PoolConfiguration.ShutdownTimeout);
            Assert.AreEqual(2500, bucketConfig.DefaultOperationLifespan);
            Assert.AreEqual(75000, config.ViewRequestTimeout);
        }

        [Test]
        public void Test_Custom()
        {
            var config = new ClientConfiguration
            {
                DefaultOperationLifespan = 123,
                PoolConfiguration = new PoolConfiguration
                {
                    MaxSize = 10,
                    MinSize = 10,
                    SendTimeout = 12000,
                    MaxCloseAttempts = 6,
                    CloseAttemptInterval = 120
                },
                ViewRequestTimeout = 5000
            };
            config.Initialize();

            Assert.AreEqual(1, config.BucketConfigs.Count);

            var bucketConfig = config.BucketConfigs.First().Value;

            IPAddress ipAddress;
            IPAddress.TryParse("127.0.0.1", out ipAddress);
            var endPoint = new IPEndPoint(ipAddress, bucketConfig.Port);
            Assert.AreEqual(endPoint, bucketConfig.GetEndPoint());

            Assert.IsEmpty(bucketConfig.Password);
            Assert.IsEmpty(bucketConfig.Username);
            Assert.AreEqual(11210, bucketConfig.Port);
            Assert.AreEqual("default", bucketConfig.BucketName);

            Assert.AreEqual(10, bucketConfig.PoolConfiguration.MaxSize);
            Assert.AreEqual(10, bucketConfig.PoolConfiguration.MinSize);
            Assert.AreEqual(2500, bucketConfig.PoolConfiguration.RecieveTimeout);
            Assert.AreEqual(2500, bucketConfig.PoolConfiguration.OperationTimeout);
            Assert.AreEqual(10000, bucketConfig.PoolConfiguration.ShutdownTimeout);
            Assert.AreEqual(12000, bucketConfig.PoolConfiguration.SendTimeout);
            Assert.AreEqual(123, bucketConfig.DefaultOperationLifespan);
            Assert.AreEqual(120, bucketConfig.PoolConfiguration.CloseAttemptInterval);
            Assert.AreEqual(6, bucketConfig.PoolConfiguration.MaxCloseAttempts);
            Assert.AreEqual(5000, config.ViewRequestTimeout);
        }

        [Test]
        public void Test_CustomBucketConfigurations()
        {
            var config = new ClientConfiguration
            {
                BucketConfigs = new Dictionary<string, BucketConfiguration>
                {
                    {"default", new BucketConfiguration
                    {
                        PoolConfiguration = new PoolConfiguration
                        {
                            MaxSize = 5,
                            MinSize = 5
                        }
                    }},
                    {"authenticated", new BucketConfiguration
                    {
                        PoolConfiguration = new PoolConfiguration
                        {
                            MaxSize = 6,
                            MinSize = 4,
                            SendTimeout = 12000
                        },
                        DefaultOperationLifespan = 123,
                        Password = "password",
                        Username = "username",
                        BucketName = "authenticated"
                    }}
                }
            };

            config.Initialize();
            Assert.AreEqual(2, config.BucketConfigs.Count);

            var bucketConfig = config.BucketConfigs.First().Value;

            IPAddress ipAddress;
            IPAddress.TryParse("127.0.0.1", out ipAddress);
            var endPoint = new IPEndPoint(ipAddress, bucketConfig.Port);
            Assert.AreEqual(endPoint, bucketConfig.GetEndPoint());

            Assert.IsEmpty(bucketConfig.Password);
            Assert.IsEmpty(bucketConfig.Username);
            Assert.AreEqual(11210, bucketConfig.Port);
            Assert.AreEqual("default", bucketConfig.BucketName);

            Assert.AreEqual(5, bucketConfig.PoolConfiguration.MaxSize);
            Assert.AreEqual(5, bucketConfig.PoolConfiguration.MinSize);
            Assert.AreEqual(2500, bucketConfig.PoolConfiguration.RecieveTimeout);
            Assert.AreEqual(2500, bucketConfig.PoolConfiguration.OperationTimeout);
            Assert.AreEqual(10000, bucketConfig.PoolConfiguration.ShutdownTimeout);
            Assert.AreEqual(2500, bucketConfig.DefaultOperationLifespan);

            //test the second configuration was taken into account as well
            bucketConfig = config.BucketConfigs.Last().Value;
            Assert.AreEqual("password", bucketConfig.Password);
            Assert.AreEqual("username", bucketConfig.Username);
            Assert.AreEqual("authenticated", bucketConfig.BucketName);

            Assert.AreEqual(6, bucketConfig.PoolConfiguration.MaxSize);
            Assert.AreEqual(4, bucketConfig.PoolConfiguration.MinSize);
            Assert.AreEqual(2500, bucketConfig.PoolConfiguration.RecieveTimeout);
            Assert.AreEqual(2500, bucketConfig.PoolConfiguration.OperationTimeout);
            Assert.AreEqual(10000, bucketConfig.PoolConfiguration.ShutdownTimeout);
            Assert.AreEqual(12000, bucketConfig.PoolConfiguration.SendTimeout);
            Assert.AreEqual(123, bucketConfig.DefaultOperationLifespan);
        }

        [Test]
        public void When_AppConfig_Used_OperationLifespan_Priority_Is_Respected()
        {
            var config = new ClientConfiguration((CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase_1"));
            config.Initialize();

            //check the global value
            Assert.AreEqual(1000, config.DefaultOperationLifespan);

            //check that if a bucket specific value is set, it supersedes global value
            var bucketConfig = config.BucketConfigs["testbucket"];
            Assert.AreEqual(2000, bucketConfig.DefaultOperationLifespan);

            //check that leaving the bucket's value to its default results in using global value
            bucketConfig = config.BucketConfigs["beer-sample"];
            Assert.AreEqual(1000, bucketConfig.DefaultOperationLifespan);
        }

        [Test]
        public void When_AppConfig_Used_PoolConfiguration_Reflects_Tuning()
        {
            var config = new ClientConfiguration((CouchbaseClientSection) ConfigurationManager.GetSection("couchbaseClients/couchbase_1"));
            config.Initialize();

            var bucketConfig = config.BucketConfigs["testbucket"];
            var bucketPoolConfig = bucketConfig.PoolConfiguration;
            Assert.AreEqual(10, bucketPoolConfig.MaxSize);
            Assert.AreEqual(5, bucketPoolConfig.MinSize);
            Assert.AreEqual(5000, bucketPoolConfig.WaitTimeout);
            Assert.AreEqual(3000, bucketPoolConfig.ShutdownTimeout);
            Assert.AreEqual(12000, bucketPoolConfig.SendTimeout);
        }

        [Test]
        public void Test_UseSsl()
        {
            var config = new ClientConfiguration {UseSsl = true};
            config.Initialize();

            var bucket = config.BucketConfigs.First().Value;
            Assert.AreEqual(true, bucket.UseSsl);
            Assert.AreEqual("https://localhost:18091/pools", config.Servers.First().ToString());

            Assert.AreEqual("127.0.0.1:11207", bucket.GetEndPoint().ToString());
        }

        [Test]
        public void Test_UseSslOnBucketDontCascade()
        {
            const string name = "IAmProtected";
            var config = new ClientConfiguration
            {
                UseSsl = false
            };
            config.BucketConfigs.Add(name, new BucketConfiguration()
            {
                BucketName = name,
                UseSsl = true
            });
            config.Initialize();


            var bucket = config.BucketConfigs.First().Value;
            Assert.AreNotEqual(name, bucket.BucketName);
            Assert.AreEqual(false, bucket.UseSsl);
            Assert.AreEqual("127.0.0.1:"+config.DirectPort, bucket.GetEndPoint().ToString());

            var protectedBucket = config.BucketConfigs[name];
            Assert.AreEqual(true, protectedBucket.UseSsl);
            Assert.AreEqual("127.0.0.1:"+config.SslPort, protectedBucket.GetEndPoint().ToString());
        }

        [Test]
        [ExpectedException(typeof(NotSupportedException))]
        public void When_NotSupportedException_Thrown_When_Proxy_Port_Is_Configured()
        {
            var configuration = new ClientConfiguration {PoolConfiguration = {MaxSize = 10, MinSize = 10}};

            configuration.Servers.Clear();
            configuration.Servers.Add(new Uri("http://127.0.0.1:8091/pools"));

            var bc = new BucketConfiguration();
            bc.Password = "secret";
            bc.Username = "admin";
            bc.BucketName = "authenticated";

            bc.Servers.Clear();
            bc.Servers.Add(new Uri("http://127.0.0.1:8091/pools"));
            bc.Port = 11211;

            configuration.BucketConfigs.Clear();
            configuration.BucketConfigs.Add("authenticated", bc);
            configuration.Initialize();
        }

        [Test]
        public void When_UseSsl_Is_False_At_Config_Level_Ssl_Is_Used()
        {
            var remoteHost = ConfigurationManager.AppSettings["bootstrapUrl"];
            var config = new ClientConfiguration()
            {
                UseSsl = true,
                Servers = new List<Uri>
                {
                    new Uri(remoteHost)
                }
            };
            var cluster = new Cluster(config);
            using (var bucket = cluster.OpenBucket())
            {
                //all buckets opened with this configuration will use SSL
                Assert.IsTrue(bucket.IsSecure);
            }
        }

        [Test]
        public void When_UseSsl_Is_False_At_Bucket_Level_Ssl_Is_Used()
        {
            var config = ClientConfigUtil.GetConfiguration();
            config.BucketConfigs = new Dictionary<string, BucketConfiguration>
            {
                {
                    "default", new BucketConfiguration
                    {
                        UseSsl = true
                    }
                }
            };
            var cluster = new Cluster(config);
            using (var bucket = cluster.OpenBucket("beer-sample"))
            {
                //only the customers bucket will use SSL
                Assert.IsTrue(bucket.IsSecure);
            }
        }

        [Test]
        public void When_UseSsl_Is_False_At_Config_Level_Ssl_Is_Not_Used()
        {
            var remoteHost = ConfigurationManager.AppSettings["bootstrapUrl"];
            var config = new ClientConfiguration()
            {
                UseSsl = false,
                Servers = new List<Uri>
                {
                    new Uri(remoteHost)
                }
            };
            var cluster = new Cluster(config);
            using (var bucket = cluster.OpenBucket())
            {
                //all buckets opened with this configuration will not use SSL
                Assert.IsFalse(bucket.IsSecure);
            }
        }

        [Test]
        public void When_UseSsl_Is_False_At_Bucket_Level_Ssl_Is_Not_Used()
        {
            var remoteHost = ConfigurationManager.AppSettings["bootstrapUrl"];
            var config = new ClientConfiguration
            {
                BucketConfigs = new Dictionary<string, BucketConfiguration>
                {
                    {"beer-sample", new BucketConfiguration
                    {
                        UseSsl = false
                    }}
                },
                Servers = new List<Uri>
                {
                    new Uri(remoteHost)
                }
            };
            var cluster = new Cluster(config);
            using (var bucket = cluster.OpenBucket("beer-sample"))
            {
                //only the customers bucket will not use SSL
                Assert.IsFalse(bucket.IsSecure);
            }
        }

        [Test]
        public void When_Servers_List_Does_Not_Change_The_BucketConfig_Is_Not_Updated()
        {
            var couchbaseConfiguration = new ClientConfiguration();
            couchbaseConfiguration.Initialize();

            Assert.AreEqual(1, couchbaseConfiguration.Servers.Count);

            var bucketConfig = couchbaseConfiguration.BucketConfigs.First().Value;
            Assert.AreEqual(1, bucketConfig.Servers.Count);
            Assert.Contains(new Uri("http://localhost:8091/pools"), bucketConfig.Servers);
        }

        [Test]
        public void When_Servers_List_Changes_The_BucketConfig_Is_Updated()
        {
            var couchbaseConfiguration = new ClientConfiguration();
            couchbaseConfiguration.Servers.Clear();
            couchbaseConfiguration.Servers.Add(new Uri("http://192.168.37.2"));
            couchbaseConfiguration.Servers.Add(new Uri("http://192.168.37.101"));
            couchbaseConfiguration.Initialize();

            Assert.AreEqual(2, couchbaseConfiguration.Servers.Count);

            var bucketConfig = couchbaseConfiguration.BucketConfigs.First().Value;
            Assert.AreEqual(2, bucketConfig.Servers.Count);
            Assert.Contains(new Uri("http://192.168.37.2"), bucketConfig.Servers);
            Assert.Contains(new Uri("http://192.168.37.101"), bucketConfig.Servers);
        }

        [Test]
        public void When_Servers_Changes_HasServerChanged_Returns_True()
        {
            var couchbaseConfiguration = new ClientConfiguration();
            couchbaseConfiguration.Servers.Clear();
            couchbaseConfiguration.Servers.Add(new Uri("http://192.168.37.2"));
            couchbaseConfiguration.Servers.Add(new Uri("http://192.168.37.101"));
            Assert.IsTrue(couchbaseConfiguration.HasServersChanged());
        }

        [Test]
        public void When_Servers_Has_Not_Changed_HasServerChanged_Returns_False()
        {
            var couchbaseConfiguration = new ClientConfiguration();
            Assert.IsFalse(couchbaseConfiguration.HasServersChanged());
        }

        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void When_Servers_Is_Empty_ArgumentNullException_Is_Thrown()
        {
            var couchbaseConfiguration = new ClientConfiguration();
            couchbaseConfiguration.Servers.Clear();
            Assert.IsTrue(couchbaseConfiguration.HasServersChanged());
        }

        [Test]
        public void When_Default_Item_has_Changed_HasServerChanged_Returns_True()
        {
            var couchbaseConfiguration = new ClientConfiguration();
            couchbaseConfiguration.Servers[0]= new Uri("http://localhost2:8091");
            Assert.IsTrue(couchbaseConfiguration.HasServersChanged());
        }

        public void When_EnableTcpKeepAlives_Is_Disabled_In_AppConfig_EnableTcpKeepAlives_Is_False()
        {
            var config = new ClientConfiguration((CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase_4"));
            config.Initialize();

            var bucket = config.BucketConfigs["default"];
            Assert.IsFalse(bucket.PoolConfiguration.EnableTcpKeepAlives);
        }

        [Test]
        public void When_EnableTcpKeepAlives_Is_Enabled_In_AppConfig_EnableTcpKeepAlives_Is_True()
        {
            var config = new ClientConfiguration((CouchbaseClientSection) ConfigurationManager.GetSection("couchbaseClients/couchbase_4"));
            config.Initialize();

            var bucket = config.BucketConfigs["default2"];
            Assert.IsTrue(bucket.PoolConfiguration.EnableTcpKeepAlives);
            Assert.AreEqual(10000, bucket.PoolConfiguration.TcpKeepAliveInterval);
            Assert.AreEqual(60000, bucket.PoolConfiguration.TcpKeepAliveTime);
        }

        [Test]
        public void When_Custom_Converter_Configured_In_AppConfig_It_Is_Returned()
        {
            var section = (CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase_2");
            var config = new ClientConfiguration(section);
            Assert.IsInstanceOf<FakeConverter>(config.Converter());
        }

        [Test]
        public void When_Custom_Transcoder_Configured_In_AppConfig_It_Is_Returned()
        {
            var section = (CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase_2");
            var config = new ClientConfiguration(section);
            Assert.IsInstanceOf<FakeTranscoder>(config.Transcoder());
        }

        [Test]
        public void When_Custom_Serializer_Configured_In_AppConfig_It_Is_Returned()
        {
            var section = (CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase_2");
            var config = new ClientConfiguration(section);
            Assert.IsInstanceOf<FakeSerializer>(config.Serializer());
        }

        [Test]
        public void When_EnhancedDurability_Is_Enabled_SupportsEnhancedDurability_Is_True()
        {
            var config = ClientConfigUtil.GetConfiguration();
            config.BucketConfigs = new Dictionary<string, BucketConfiguration>
            {
                {
                    "default", new BucketConfiguration
                    {
                        UseEnhancedDurability = true
                    }
                }
            };

            using (var cluster = new Cluster(config))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    Assert.IsTrue(bucket.SupportsEnhancedDurability);
                }
            }
        }

        [Test]
        public void When_EnhancedDurability_Is_Not_Enabled_SupportsEnhancedDurability_Is_False()
        {
            var config = ClientConfigUtil.GetConfiguration();
            config.BucketConfigs = new Dictionary<string, BucketConfiguration>
            {
                {
                    "default", new BucketConfiguration
                    {
                        UseEnhancedDurability = false
                    }
                }
            };
            using (var cluster = new Cluster(config))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    Assert.IsFalse(bucket.SupportsEnhancedDurability);
                }
            }
        }

        [Test]
        public void When_Default_Configuration_Is_Used_SupportsEnhancedDurability_Is_False()
        {
            using (var cluster = new Cluster(ClientConfigUtil.GetConfiguration()))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    Assert.IsFalse(bucket.SupportsEnhancedDurability);
                }
            }
        }

        [Test]
        public void When_DefaultConnectionLimit_Is_10_ServicePoint_ConnectionLimit_Is_10()
        {
            var config = new ClientConfiguration
            {
                DefaultConnectionLimit = 10,
                Servers = new List<Uri>
                {
                    new Uri("http://10.141.111.108:8091/")
                }
            };
            config.Initialize();

            var servicePoint = ServicePointManager.FindServicePoint(config.Servers.First());
            Assert.AreEqual(servicePoint.ConnectionLimit, 10);
        }
    }
}

#region [ License information          ]

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
