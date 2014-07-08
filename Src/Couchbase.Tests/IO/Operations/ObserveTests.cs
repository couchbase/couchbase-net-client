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
    public class ObserveTests : OperationTestBase
    {
        [Test]
        public void Test_Observe()
        {
            const string key = "Test_Observe";

            var operation = new Observe(key, GetVBucket(), new AutoByteConverter());
            var result = IOStrategy.Execute(operation);
            Console.WriteLine(result.Message);
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void Test_Observe2()
        {
            const string key = "Test_Observe";

            var operation = new Observe(key, GetVBucket(), new AutoByteConverter());
            var result = IOStrategy.Execute(operation);
            Console.WriteLine(result.Message);
            Console.WriteLine(result.Value);
            Assert.IsTrue(result.Success);
        }
    }
}
