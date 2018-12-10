using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Monitoring;
using Couchbase.Configuration.Server.Providers;
using Couchbase.Core;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests.Configuration.Server.Monitoring
{
    [TestFixture()]
    public class ConfigMonitorTests
    {
        [Test, Ignore("Intermittently fails on Window :(")]
        public async Task Test_StartMonitoring()
        {
            var clientConfig = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri("http://10.111.160.101:8091"),
                    new Uri("http://10.111.160.102:8091"),
                    new Uri("http://10.111.160.104:8091")
                },
                BucketConfigs = new Dictionary<string, BucketConfiguration>
                {
                    {
                        "default", new BucketConfiguration
                        {
                            BucketName = "default",
                            Username = "default",
                            Password = ""
                        }
                    },
                    {
                        "travel-sample", new BucketConfiguration
                        {
                            BucketName = "travel-sample",
                            Username = "travel-sample",
                            Password = ""
                        }
                    },
                },
                ConfigPollInterval = 500,
                ConfigPollCheckFloor = 0,
                OperationTracingEnabled = false,
                OrphanedResponseLoggingEnabled = false
            };
            clientConfig.Initialize();

            var controller = new Mock<IClusterController>();
            controller.Setup(x => x.Configuration).Returns(clientConfig);
            controller.Setup(x => x.ConfigProviders).Returns(new List<IConfigProvider>());

            var cts = new CancellationTokenSource();

            using (var monitor = new ConfigMonitor(controller.Object, cts))
            {
                monitor.StartMonitoring();

                await Task.Delay(5000);
                controller.VerifySet(x => controller.Object.LastConfigCheckedTime = It.IsAny<DateTime>(), Times.AtLeast(1));
            }
        }

        [Test]
        public void Test_Dispose()
        {
            var clientConfig = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri("http://10.111.160.101:8091"),
                    new Uri("http://10.111.160.102:8091"),
                    new Uri("http://10.111.160.104:8091")
                },
                BucketConfigs = new Dictionary<string, BucketConfiguration>
                {
                    {
                        "default", new BucketConfiguration
                        {
                            BucketName = "default",
                            Username = "default",
                            Password = ""
                        }
                    },
                    {
                        "travel-sample", new BucketConfiguration
                        {
                            BucketName = "travel-sample",
                            Username = "travel-sample",
                            Password = ""
                        }
                    }
                },
                HeartbeatConfigInterval = 1000
            };
            clientConfig.Initialize();
            var configProvider = new Mock<IConfigProvider>();
            var controller = new Mock<IClusterController>();
            controller.Setup(x => x.Configuration).Returns(clientConfig);
            controller.Setup(x => x.ConfigProviders).Returns(new List<IConfigProvider> { configProvider.Object });

            var cts = new CancellationTokenSource();
            var monitor = new ConfigMonitor(controller.Object, cts);
            monitor.StartMonitoring();
            Thread.Sleep(1500);
            monitor.Dispose();
            Assert.IsTrue(cts.IsCancellationRequested);
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
