using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Views;
using NUnit.Framework;

namespace Couchbase.Tests.Configuration.Client
{
    [TestFixture]
    public class ClientConfigurationTests
    {
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
        }

        [Test]
        public void Test_Custom()
        {
            var config = new ClientConfiguration
            {
                PoolConfiguration = new PoolConfiguration
                {
                    MaxSize = 10,
                    MinSize = 10
                }
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
                            MinSize = 4
                        },
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
            var remoteHost = ConfigurationManager.AppSettings["bootstrapUrl"];
            var config = new ClientConfiguration
            {
                BucketConfigs = new Dictionary<string, BucketConfiguration>
                {
                    {"beer-sample", new BucketConfiguration
                    {
                        UseSsl = true
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
