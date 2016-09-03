using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Utils;
using Moq;
using NUnit.Framework;

namespace Couchbase.IntegrationTests.Utils
{
    [TestFixture]
    public class UriExtensionsTests
    {
        #region GetIpAddress

        [Test]
        public void GetIpAddress_DnsEntry_ReturnsIp()
        {
            // Arrange

            var uri = new Uri("http://localhost/");

            // Act

            var result = uri.GetIpAddress(false);

            // Assert

            Assert.AreEqual(new IPAddress(new byte[] {127, 0, 0, 1}), result);
        }

        [Test]
        public void GetIpAddress_DnsEntry_NoDeadlock()
        {
            // Using an asynchronous view query within an MVC Web API action causes
            // a deadlock if you wait for the result synchronously.

            var context = new Mock<SynchronizationContext>
            {
                CallBase = true
            };

            SynchronizationContext.SetSynchronizationContext(context.Object);
            try
            {
                var uri = new Uri("http://localhost/");

                uri.GetIpAddress(false);

                // If view queries are incorrectly awaiting on the current SynchronizationContext
                // We will see calls to Post or Send on the mock

                context.Verify(m => m.Post(It.IsAny<SendOrPostCallback>(), It.IsAny<object>()), Times.Never);
                context.Verify(m => m.Send(It.IsAny<SendOrPostCallback>(), It.IsAny<object>()), Times.Never);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        #endregion
    }
}
