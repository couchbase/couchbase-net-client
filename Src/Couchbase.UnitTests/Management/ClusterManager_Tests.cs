using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server;
using Couchbase.Management;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests.Management
{
    [TestFixture]
    public class ClusterManagerTests
    {
        private ClientConfiguration _clientConfiguration;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            _clientConfiguration = new ClientConfiguration()
            {
                BucketConfigs = new Dictionary<string, BucketConfiguration>()
                {
                    { "test", new BucketConfiguration() }
                }
            };
        }

        #region CreateBucket

        [Test]
        public void CreateBucket_FlushEnabledTrue_SendsWithCorrectParameter()
        {
            // Arrange

            var mockServerConfig = new Mock<IServerConfig>();

            var managerMock = new Mock<ClusterManager>(_clientConfiguration, mockServerConfig.Object,
                new HttpClient(), new Views.JsonDataMapper(_clientConfiguration), "username", "password");
            managerMock
                .Setup(x => x.PostFormDataAsync(It.IsAny<Uri>(), It.Is<Dictionary<string, string>>(p => p["flushEnabled"] == "1")))
                .Returns(Task.FromResult((IResult) new DefaultResult(true, "success", null)));

            // Act

            managerMock.Object.CreateBucket("test", flushEnabled: true);

            // Assert

            managerMock.Verify(
                x => x.PostFormDataAsync(It.IsAny<Uri>(), It.Is<Dictionary<string, string>>(p => p["flushEnabled"] == "1")),
                Times.Once);
        }

        [Test]
        public void CreateBucket_FlushEnabledFalse_SendsWithCorrectParameter()
        {
            // Arrange

            var mockServerConfig = new Mock<IServerConfig>();

            var managerMock = new Mock<ClusterManager>(_clientConfiguration, mockServerConfig.Object,
                new HttpClient(), new Views.JsonDataMapper(_clientConfiguration), "username", "password");
            managerMock
                .Setup(x => x.PostFormDataAsync(It.IsAny<Uri>(), It.Is<Dictionary<string, string>>(p => p["flushEnabled"] == "0")))
                .Returns(Task.FromResult((IResult)new DefaultResult(true, "success", null)));

            // Act

            managerMock.Object.CreateBucket("test", flushEnabled: false);

            // Assert

            managerMock.Verify(
                x => x.PostFormDataAsync(It.IsAny<Uri>(), It.Is<Dictionary<string, string>>(p => p["flushEnabled"] == "0")),
                Times.Once);
        }

        #endregion
    }
}