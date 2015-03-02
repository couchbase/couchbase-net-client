using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Operations
{
    [TestFixture]
    public class NoopOperationTests : OperationTestBase
    {
        [Test]
        public void Test_NoOp()
        {
            var noop = new Noop(new AutoByteConverter(), OperationLifespanTimeout);
            var result = IOStrategy.Execute(noop);
            Assert.IsTrue(result.Success);
        }
    }
}
