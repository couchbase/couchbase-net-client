using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.IO.Operations;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Operations
{
    [TestFixture]
    public class ConfigTests : OperationTestBase
    {
        [Test]
        public void Test_Config()
        {
            var config = new Config(Converter);
            var result = IOStrategy.Execute(config);
            Assert.IsTrue(result.Success);
        }
    }
}
