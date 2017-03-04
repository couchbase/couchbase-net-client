using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Monitoring;
using Couchbase.Logging;
using Couchbase.N1QL;
using Moq;
using Moq.Protected;
using NUnit.Framework;

namespace Couchbase.UnitTests.Core.Monitoring
{
    [TestFixture]
    public class UriTesterTests
    {
        [Test]
        public async Task UriTester_Success_ClearsFailure()
        {
            // Arrange

            var log = new Mock<ILog>();

            var messageHandler = new Mock<HttpMessageHandler>();
            messageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK
                }));

            var uriTester = new Mock<UriTesterBase>(new HttpClient(messageHandler.Object), log.Object)
            {
                CallBase = true
            };

            uriTester.Protected()
                .Setup<Uri>("GetPingUri", ItExpr.IsAny<FailureCountingUri>())
                .Returns((FailureCountingUri uri) => uri);

            var testUri = new FailureCountingUri("http://localhost:8091/");
            testUri.IncrementFailed();
            testUri.IncrementFailed();

            // Act

            await uriTester.Object.TestUri(testUri, CancellationToken.None);

            // Assert

            Assert.True(testUri.IsHealthy(1));
        }

        [Test]
        public async Task UriTester_Failure_DoesNotChangeFailureCount()
        {
            // Arrange

            var log = new Mock<ILog>();

            var messageHandler = new Mock<HttpMessageHandler>();
            messageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.ServiceUnavailable
                }));

            var uriTester = new Mock<UriTesterBase>(new HttpClient(messageHandler.Object), log.Object)
            {
                CallBase = true
            };

            uriTester.Protected()
                .Setup<Uri>("GetPingUri", ItExpr.IsAny<FailureCountingUri>())
                .Returns((FailureCountingUri uri) => uri);

            var testUri = new FailureCountingUri("http://localhost:8091/");
            testUri.IncrementFailed();
            testUri.IncrementFailed();

            // Act

            await uriTester.Object.TestUri(testUri, CancellationToken.None);

            // Assert

            Assert.False(testUri.IsHealthy(2));
            Assert.True(testUri.IsHealthy(3));
        }
    }
}
