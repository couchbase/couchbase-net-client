using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.KeyValue;
using Couchbase.Test.Common.Utils;
using Couchbase.Transactions.Config;
using Couchbase.Transactions.DataAccess;
using Couchbase.Transactions.Error;
using Couchbase.Transactions.Error.External;
using Couchbase.Transactions.Internal;
using Couchbase.Transactions.LogUtil;
using Couchbase.Transactions.Tests.UnitTests.Mocks;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;


namespace Couchbase.Transactions.Tests.UnitTests
{
    public class LoggerTests
    {
        private readonly ITestOutputHelper outputHelper;

        public LoggerTests(ITestOutputHelper outputHelper)
        {
            this.outputHelper = outputHelper;
        }

        [Fact]
        public void TransactionLogger_Redacts_Properly()
        {
            var testOutputLogger = new TestOutputLogger(this.outputHelper, nameof(LoggerTests));
            var config = new TransactionConfigImmutable(
                ExpirationTime: TimeSpan.FromMinutes(10),
                CleanupLostAttempts: false,
                CleanupClientAttempts: false,
                CleanupWindow: TimeSpan.FromSeconds(10),
                KeyValueTimeout: TimeSpan.FromSeconds(10),
                DurabilityLevel: DurabilityLevel.Majority,
                LoggerFactory: null,
                MetadataCollection: null
                );
            var ctx = new TransactionContext(Guid.NewGuid().ToString(), DateTimeOffset.UtcNow, config, null);
            var loggerFactory = new TransactionsLoggerFactory(new TestOutputLoggerFactory(this.outputHelper), ctx);
            var transactionsLogger = loggerFactory.CreateLogger(nameof(TransactionLogger_Redacts_Properly));
            var redactor = new TestRedactor();
            transactionsLogger.LogDebug("Example UD: {id}", redactor.UserData("default:_default:_default" + Guid.NewGuid().ToString()));
            var logs = ctx.Logs.ToList();
            Assert.NotEmpty(logs);
            Assert.Collection(logs, s => s.Contains("</ud>"));
        }
    }

    public class TestRedactor : Core.Logging.IRedactor
    {
        [return: NotNullIfNotNull("message")]
        public object MetaData(object message) => "<md>" + message + "</md>";

        [return: NotNullIfNotNull("message")]
        public object SystemData(object message) => "<sd>" + message + "</sd>";

        [return: NotNullIfNotNull("message")]
        public object UserData(object message) => "<ud>" + message + "</ud>";
    }
}
