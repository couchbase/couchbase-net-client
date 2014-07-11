using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Serializers;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.Views;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Operations
{
    [TestFixture]
    public class DecrementTests : OperationTestBase
    {
        [Test]
        public void Test_DecrementOperation()
        {
            const string key = "Test_DecrementOperation";

            //delete key if exists
            var delete = new Delete(key, GetVBucket(), Converter, Serializer);
            var result = IOStrategy.Execute(delete);
            Console.WriteLine("Deleting key {0}: {1}", key, result.Success);

            //increment the key
            var operation = new Increment(key, 1, 1, 0, GetVBucket(), Converter, Serializer);
            var result1 = IOStrategy.Execute(operation);
            Assert.IsTrue(result1.Success);
            Assert.AreEqual(result1.Value, 1);

            //key should be 1
            var get = new Get<string>(key, GetVBucket(), Converter, Serializer);
            var result3 = IOStrategy.Execute(get);
            Assert.AreEqual(result1.Value.ToString(CultureInfo.InvariantCulture), result3.Value);

            //decrement the key
            var decrement = new Decrement(key, 1, 1, 0, GetVBucket(), Converter, Serializer);
            var result2 = IOStrategy.Execute(decrement);
            Assert.IsTrue(result2.Success);
            Assert.AreEqual(result2.Value, 0);

            //key should be 0
            get = new Get<string>(key, GetVBucket(), Converter, Serializer);
            result3 = IOStrategy.Execute(get);
            Assert.AreEqual(0.ToString(CultureInfo.InvariantCulture), result3.Value);
        }
    }
}
