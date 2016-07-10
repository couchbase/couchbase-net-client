using System;
using System.Threading;
using Common.Logging;
using Couchbase.Core.Diagnostics;
using Couchbase.N1QL;
using Couchbase.Tests.Fakes;
using NUnit.Framework;

namespace Couchbase.Tests.N1QL
{
    [TestFixture]
    public class QueryTimerTests
    {
        private const string LogDateFormat = "yyyy/MM/dd HH:mm:ss:fff";
        private const string Statement = "\"SELECT * FROM default\"";
        private const string ServerElapsedTime = "3.764454ms";

        [TestCase]
        public void When_EnableQueryTiming_Is_False_Nothing_Is_Logged()
        {
            var queryRequest = new QueryRequest(Statement);
            var log = new FakeLog("mylogger", LogLevel.All, true, true, true, LogDateFormat);
            using (var timer = new QueryTimer(queryRequest, new CommonLogStore(log), false))
            {
                Thread.Sleep(TimeSpan.FromSeconds(1));
                timer.ClusterElapsedTime = ServerElapsedTime;
            }

            var logOutput = log.LogStore.ToString();
            Assert.IsEmpty(logOutput);
        }

        [TestCase]
        public void When_EnableQueryTiming_Is_True_And_LogLevel_Is_Not_Configured_Nothing_Is_Logged()
        {
            var queryRequest = new QueryRequest(Statement);
            var log = new FakeLog("mylogger", LogLevel.Off, true, true, true, LogDateFormat);
            using (var timer = new QueryTimer(queryRequest, new CommonLogStore(log), true))
            {
                Thread.Sleep(TimeSpan.FromSeconds(1));
                timer.ClusterElapsedTime = ServerElapsedTime;
            }

            var logOutput = log.LogStore.ToString();
            Assert.IsEmpty(logOutput);
        }

        [TestCase]
        public void When_EnableQueryTiming_Is_True_And_LogLevel_Configured_QueryTiming_Is_Logged()
        {
            var queryRequest = new QueryRequest(Statement);
            var log = new FakeLog("mylogger", LogLevel.Debug, true, true, true, LogDateFormat);
            using (var timer = new QueryTimer(queryRequest, new CommonLogStore(log), true))
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(25));
                timer.ClusterElapsedTime = ServerElapsedTime;
            }

            var logOutput = log.LogStore.ToString();
            Assert.IsTrue(logOutput.Contains("[INFO]"));
            Assert.IsTrue(logOutput.Contains("Query Timing"));
            Assert.IsTrue(logOutput.Contains(ServerElapsedTime));
            Assert.IsTrue(logOutput.Contains(Statement));
        }

        [TestCase]
        public void ServerExecutionTime_Defaults_To_NotRecorded()
        {
            var queryRequest = new QueryRequest(Statement);
            var log = new FakeLog("mylogger", LogLevel.Debug, true, true, true, LogDateFormat);
            using (new QueryTimer(queryRequest, new CommonLogStore(log), true))
            {
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }

            var logOutput = log.LogStore.ToString();
            Assert.IsTrue(logOutput.Contains("[INFO]"));
            Assert.IsTrue(logOutput.Contains("Query Timing"));
            Assert.IsTrue(logOutput.Contains(QueryTimer.NotRecorded));
            Assert.IsTrue(logOutput.Contains(Statement));
        }

        [TestCase]
        public void Throws_ArgumentException_When_QueryRequest_Statement_Is_Null_Or_Empty()
        {
            QueryRequest queryRequest;
            var log = new FakeLog("mylogger", LogLevel.Debug, true, true, true, LogDateFormat);

            queryRequest = new QueryRequest(null);
            Assert.Throws<ArgumentException>(() => new QueryTimer(queryRequest, new CommonLogStore(log), true),
                QueryTimer.QueryStatementMustBeProvided);


            queryRequest = new QueryRequest(string.Empty);
            Assert.Throws<ArgumentException>(() => new QueryTimer(queryRequest, new CommonLogStore(log), true),
                QueryTimer.QueryStatementMustBeProvided);
        }

        [TestCase]
        public void Throws_ArgumentException_When_QueryRequest_Is_Null()
        {
            var log = new FakeLog("mylogger", LogLevel.Debug, true, true, true, LogDateFormat);

            Assert.Throws<ArgumentException>(() => new QueryTimer(null, new CommonLogStore(log), true),
                QueryTimer.QueryMustBeProvided);
        }
    }
}
