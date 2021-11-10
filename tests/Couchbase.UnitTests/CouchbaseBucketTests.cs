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

namespace Couchbase.UnitTests
{
    public class CouchbaseBucketTests
    {
        [Fact]
        public void Scope_NotFound_Throws_ScopeNoteFoundException( )
        {
            var bucket = new CouchbaseBucket("default",
                new ClusterContext
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
                new BestEffortRetryStrategy());

                Assert.ThrowsAsync<ScopeNotFoundException>(async () => await bucket.ScopeAsync("doesnotexist"));
        }
    }
}
