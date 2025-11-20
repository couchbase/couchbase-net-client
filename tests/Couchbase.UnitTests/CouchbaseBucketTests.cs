using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Microsoft.Extensions.Logging;
using Moq;
using Couchbase.Management.Collections;
using Xunit;
using Couchbase.Core.Configuration.Server;

namespace Couchbase.UnitTests
{
    public class CouchbaseBucketTests
    {
        [Fact]
        public async Task Scope_DoesNotThrow_ScopeNoteFoundException()
        {
            var bucket = new CouchbaseBucket("default",
                new ClusterContext(null, new ClusterOptions().WithPasswordAuthentication("username", "password"))
                {
                    SupportsCollections = true
                },
                new Mock<IScopeFactory>().Object,
                new Mock<IRetryOrchestrator>().Object,
                new Mock<IVBucketKeyMapperFactory>().Object,
                new Mock<ILogger<CouchbaseBucket>>().Object,
                new TypedRedactor(RedactionLevel.None),
                new Mock<IBootstrapperFactory>().Object,
                NoopRequestTracer.Instance,
                new Mock<IOperationConfigurator>().Object,
                new BestEffortRetryStrategy(),
                new BucketConfig(),
                new Mock<IConfigPushHandlerFactory>().Object);

            bucket.Scope("doesnotexist");
            await bucket.ScopeAsync("doesnotexist");
        }
    }
}
