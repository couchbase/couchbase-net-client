#nullable enable
using System;
using System.Collections.Generic;
using Couchbase.Client.Transactions;
using Couchbase.Client.Transactions.Config;
using Couchbase.Client.Transactions.Error;
using Couchbase.Client.Transactions.Error.Attempts;
using Couchbase.Client.Transactions.Error.External;
using Couchbase.Core.Exceptions.Query;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
using Couchbase.Query;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Transactions;

public class ConvertQueryErrorTests
{
    private static AttemptContext CreateContext()
    {
        // NonStreamingSerializerWrapper.FromCluster resolves the cluster transcoder.
        var services = new Mock<IServiceProvider>();
        services.Setup(s => s.GetService(typeof(ITypeTranscoder))).Returns(new JsonTranscoder());
        var cluster = new Mock<ICluster>();
        cluster.Setup(c => c.ClusterServices).Returns(services.Object);

        return new AttemptContext(
            overallContext: new TransactionContext("txn-1", DateTimeOffset.UtcNow, new TransactionsConfig(), null),
            attemptId: "attempt-1",
            testHooks: null,
            redactor: new Redactor(RedactionLevel.None),
            loggerFactory: NullLoggerFactory.Instance,
            cluster: cluster.Object);
    }

    private static CouchbaseException QueryError(int code) =>
        new(new QueryErrorContext
        {
            Errors = new List<Error> { new() { Code = code, Message = "timeout" } }
        });

    // Spec (transactions-query.md, "Error handling"): 1080 or 17010 raises
    // Error(cause=AttemptExpired(err), raise=TRANSACTION_EXPIRED, rollback=false).
    [Theory]
    [InlineData(1080)]
    [InlineData(17010)]
    public void QueryTimeout_RaisesExpiredWithoutRollback(int code)
    {
        var converted = CreateContext().ConvertQueryError(QueryError(code));

        var tofe = Assert.IsType<TransactionOperationFailedException>(converted);
        Assert.Equal(ErrorClass.FailExpiry, tofe.CausingErrorClass);
        Assert.Equal(TransactionOperationFailedException.FinalError.TransactionExpired, tofe.FinalErrorToRaise);
        Assert.False(tofe.AutoRollbackAttempt);
        Assert.False(tofe.RetryTransaction);
        Assert.IsType<AttemptExpiredException>(tofe.Cause);
    }
}
