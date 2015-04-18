using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.Core.Diagnostics;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Operations;
using Couchbase.Tests.Fakes;
using NUnit.Framework;

namespace Couchbase.Tests.Core.Diagnostics
{
    [TestFixture]
    public class OperationTimerTests
    {
        private static readonly ILog Log = LogManager.GetLogger<OperationTimerTests>();
        private ITypeTranscoder _transcoder = new DefaultTranscoder();

        private const uint OperationLifespan = 2500; //ms

        [Test]
        public void Test_Integrated_With_Common_Log()
        {
            var op = new Get<string>("key", null, _transcoder, OperationLifespan);
            using (new OperationTimer(TimingLevel.One, op, new CommonLogStore(Log)))
            {
                Thread.Sleep(1000);
            }
        }

        [Test]
        public void When_TimingLevel_Is_One_Log_Message_Contains_One()
        {
            var log = new FakeLog("mylogger", LogLevel.Debug, true, true, true, "yyyy/MM/dd HH:mm:ss:fff");
            var op = new Get<string>("key", null, _transcoder, OperationLifespan);
            using (new OperationTimer(TimingLevel.One, op, new CommonLogStore(log)))
            {
                Thread.Sleep(100);
            }
            var loggedString = log.LogStore.ToString();
            Assert.IsTrue(loggedString.Contains("One"));
        }

        [Test]
        public void When_TimingLevel_Is_Two_Log_Message_Contains_Two()
        {
            var log = new FakeLog("mylogger", LogLevel.Debug, true, true, true, "yyyy/MM/dd HH:mm:ss:fff");
            var op = new Get<string>("key", null, _transcoder, OperationLifespan);
            using (new OperationTimer(TimingLevel.Two, op, new CommonLogStore(log)))
            {
                Thread.Sleep(100);
            }
            var loggedString = log.LogStore.ToString();
            Assert.IsTrue(loggedString.Contains("Two"));
        }

        [Test]
        public void When_TimingLevel_Is_Three_Log_Message_Contains_Three()
        {
            var log = new FakeLog("mylogger", LogLevel.Debug, true, true, true, "yyyy/MM/dd HH:mm:ss:fff");
            var op = new Get<string>("key", null, _transcoder, OperationLifespan);
            using (new OperationTimer(TimingLevel.Three, op, new CommonLogStore(log)))
            {
                Thread.Sleep(100);
            }
            var loggedString = log.LogStore.ToString();
            Assert.IsTrue(loggedString.Contains("Three"));
        }

        [Test]
        public void When_TimingLevel_Is_None_Log_Message_Contains_No_Level()
        {
            var log = new FakeLog("mylogger", LogLevel.Info, true, true, true, "yyyy/MM/dd HH:mm:ss:fff");
            var op = new Get<string>("key", null, _transcoder, OperationLifespan);
            using (new OperationTimer(TimingLevel.None, op, new CommonLogStore(log)))
            {
                Thread.Sleep(100);
            }
            var loggedString = log.LogStore.ToString();
            Assert.IsFalse(loggedString.Contains("Level"));
        }

        [Test]
        public void When_LogLevel_Is_Off_Nothing_Is_Logged()
        {
            var log = new FakeLog("mylogger", LogLevel.Off, true, true, true, "yyyy/MM/dd HH:mm:ss:fff");
            var op = new Get<string>("key", null, _transcoder, OperationLifespan);
            using (new OperationTimer(TimingLevel.None, op, new CommonLogStore(log)))
            {
                Thread.Sleep(100);
            }
            var loggedString = log.LogStore.ToString();
            Assert.IsEmpty(loggedString);
        }
    }
}
