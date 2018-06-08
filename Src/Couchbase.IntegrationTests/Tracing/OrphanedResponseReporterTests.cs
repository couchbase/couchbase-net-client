using System;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Utils;
using Couchbase.Tracing;
using NUnit.Framework;

namespace Couchbase.IntegrationTests.Tracing
{
    [TestFixture]
    public class OrphanedResponseReporterTests
    {
#if NET452
        [Test]
        public void Test_Configuration_Defauts()
        {
            var config = TestConfiguration.GetCurrentConfiguration();

            Assert.IsFalse(config.OperationTracingEnabled);
            Assert.IsFalse(config.OrphanedResponseLoggingEnabled);
        }
#endif
        [Test, Ignore("only used for local testing")]
        public async Task Test_logging_output()
        {
            var random = new Random();
            var reporter = new OrphanedResponseLogger
            {
                Interval = 100
            };

            foreach (var i in Enumerable.Range(1, 100))
            {
                var context = OperationContext.CreateKvContext((uint) i);
                context.ConnectionId = "connection-id";
                context.BucketName = "default";
                context.LocalEndpoint = "192.168.1.1:16212";
                context.RemoteEndpoint = "cb-1.domain.com";
                context.ServerDuration = random.Next(10, 100);
                reporter.Add(context);

                await Task.Delay(10);
            }

            await Task.Delay(10000);
            reporter.Dispose();
        }
    }
}
