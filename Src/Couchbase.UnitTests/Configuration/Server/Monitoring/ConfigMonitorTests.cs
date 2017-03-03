using System;
using System.Collections.Generic;
using System.Threading;
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
        [Test]
        public void Test_StartMonitoring()
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
                }
            };
            clientConfig.Initialize();
            var configProvider = new Mock<IConfigProvider>();
            var controller = new Mock<IClusterController>();
            controller.Setup(x => x.Configuration).Returns(clientConfig);
            controller.Setup(x => x.ConfigProviders).Returns(new List<IConfigProvider> { configProvider.Object });

            var cts = new CancellationTokenSource();
            var monitor = new ConfigMonitor(controller.Object, cts);
            monitor.StartMonitoring();
            cts.CancelAfter(1000);
            Thread.Sleep(2500);
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
