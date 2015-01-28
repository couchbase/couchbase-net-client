using System.Threading;
using Common.Logging;
using Couchbase.Core.Diagnostics;
using Couchbase.IO.Operations;
using NUnit.Framework;

namespace Couchbase.Tests.Core.Diagnostics
{
    [TestFixture]
    public class TimerFactoryTests
    {
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();

        [Test]
        public void Test_GetFactory()
        {
            var timer = TimingFactory.GetTimer(Log);
            using (timer(TimingLevel.One, new Get<string>(null, null, null, null)))
            {
                Thread.Sleep(100);
            }

            using (timer(TimingLevel.Two, new Get<string>(null, null, null, null)))
            {
                Thread.Sleep(100);
            }
        }
    }
}
