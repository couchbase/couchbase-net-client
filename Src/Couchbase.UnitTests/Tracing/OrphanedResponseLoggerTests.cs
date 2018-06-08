using System.Linq;
using System.Threading.Tasks;
using Couchbase.Tracing;
using NUnit.Framework;

namespace Couchbase.UnitTests.Tracing
{
    [TestFixture]
    public class OrphanedResponseLoggerTests
    {
        [Test]
        public async Task Can_add_lots_of_operation_contexts_concurrently()
        {
            var tracer = new OrphanedResponseLogger();

            var tasks = Enumerable.Range(1, 1000).Select(x =>
            {
                tracer.Add(new OperationContext(CouchbaseTags.ServiceKv));
                return Task.FromResult(true);
            });

            // schedule all the tasks using threadpool
            await Task.WhenAll(tasks);

            // wait for queue to flush
            await Task.Delay(1000);

            // check all items made it into sample
            Assert.AreEqual(1000, tracer.TotalCount);
        }
    }
}
