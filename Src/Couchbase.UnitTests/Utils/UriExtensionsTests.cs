using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Utils;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests.Utils
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

        #region ReplaceCouchbaseSchemeWithHttp

        [Test]
        [TestCase(true, "https")]
        [TestCase(false, "http")]
        public void ReplaceCouchbaseSchemeWithHttp_Root_Configuration(bool useSsl, string expectedScheme)
        {
            //arrange
            var config = new ClientConfiguration
            {
                UseSsl = useSsl
            };
            var uri = new UriBuilder("couchbase", "localhost", 8091).Uri;

            //act
            var actualSceheme = uri.ReplaceCouchbaseSchemeWithHttp(config, "travel-sample").Scheme;

            //assert
            Assert.AreEqual(expectedScheme, actualSceheme);
        }

        [Test]
        [TestCase(true, "https")]
        [TestCase(false, "http")]
        public void ReplaceCouchbaseSchemeWithHttp_Bucket_Configuration(bool useSsl, string expectedScheme)
        {
            //arrange
            var config = new ClientConfiguration
            {
               BucketConfigs = new Dictionary<string, BucketConfiguration>
               {
                   {"travel-sample", new BucketConfiguration
                   {
                       UseSsl = useSsl
                   } }
               }
            };
            var uri = new UriBuilder("couchbase", "localhost", 8091).Uri;

            //act
            var actualScheme = uri.ReplaceCouchbaseSchemeWithHttp(config, "travel-sample").Scheme;

            //assert
            Assert.AreEqual(expectedScheme, actualScheme);
        }

        [TestCase(8091)]
        [TestCase(12345)]
        public void ReplaceCouchbaseSchemeWithHttp_sets_port_to_config_management_port(int expectedPort)
        {
            //arrange
            var config = new ClientConfiguration {MgmtPort = expectedPort };
            var uri = new UriBuilder("couchbase", "localhost").Uri;

            //act
            var actualPort = uri.ReplaceCouchbaseSchemeWithHttp(config, "travel-sample").Port;

            //assert
            Assert.AreEqual(expectedPort, actualPort);
        }

        #endregion
    }
}
