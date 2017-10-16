using System;
using System.Net;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Providers.CarrierPublication;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests.Core
{
    [TestFixture]
    public class ClusterControllerTests
    {
        [Test]
        [TestCase("memcached")]
        public void CreateBucket_Throws_BootstrapException(string bucketType)
        {
            var mockConnectionPool = new Mock<IConnectionPool>();
            mockConnectionPool.Setup(x => x.EndPoint)
                .Returns(new IPEndPoint(IPAddress.Any, 0));
            var mockSaslMech = new Mock<ISaslMechanism>();
            var mockIOService = new Mock<IIOService>();
            mockIOService.Setup(x => x.ConnectionPool)
                .Returns(mockConnectionPool.Object);
            mockIOService.Setup(x => x.SupportsEnhancedAuthentication)
                .Returns(false);
            mockIOService.Setup(x => x.Execute(It.IsAny<IOperation>()))
                .Returns(new OperationResult { Success = true });
            mockIOService.Setup(x => x.Execute(It.IsAny<IOperation<BucketConfig>>()))
                .Returns(new OperationResult<BucketConfig>
                {
                    Success = false,
                    Status = ResponseStatus.UnknownCommand
                });

            var config = new ClientConfiguration();
            var provider = new CarrierPublicationProvider(
                config,
                pool => mockIOService.Object,
                (c, e) => mockConnectionPool.Object,
                (ac, b, c, d) => mockSaslMech.Object,
                new DefaultConverter(),
                new DefaultTranscoder()
            );

            var controller = new ClusterController(
                new ClientConfiguration { EnableDeadServiceUriPing = false, ConfigPollEnabled = false },
                p => mockIOService.Object,
                (c, i) => mockConnectionPool.Object,
                (u, p, cp, t) => mockSaslMech.Object,
                new DefaultConverter(),
                new DefaultTranscoder());

            controller.ConfigProviders.Clear();
            controller.ConfigProviders.Add(provider);

            try
            {
                controller.CreateBucket("default");
                Assert.Fail("Test failed if CreateBucket does not throw exception.");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOf<BootstrapException>(e);
                Assert.IsTrue(e.Message.StartsWith("Could not bootstrap - check inner exceptions for details."));
                Assert.IsInstanceOf<ConfigException>(e.InnerException);
            }
        }
    }
}

#region [ License information          ]
/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
