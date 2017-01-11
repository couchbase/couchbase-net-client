using System.Threading;
using Couchbase.Logging;
using Couchbase.Core.Diagnostics;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Operations;
using NUnit.Framework;

namespace Couchbase.Tests.Core.Diagnostics
{
    [TestFixture]
    public class TimerFactoryTests
    {
        private readonly static ILog Log = LogManager.GetLogger<TimerFactoryTests>();

        [Test]
        public void Test_GetFactory()
        {
            var timer = TimingFactory.GetTimer(Log);
            using (timer(TimingLevel.One, new Get<string>(null, null, new DefaultTranscoder(), 500)))
            {
                Thread.Sleep(100);
            }

            using (timer(TimingLevel.Two, new Get<string>(null, null, new DefaultTranscoder(), 500)))
            {
                Thread.Sleep(100);
            }
        }
    }
}
