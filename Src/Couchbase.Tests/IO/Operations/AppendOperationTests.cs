using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Serializers;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Operations
{
    public class AppendOperationTests : OperationTestBase
    {
        [Test]
        public void When_Key_Exists_Append_Succeeds()
        {
            var converter = new AutoByteConverter();
            var serializer = new TypeSerializer(converter);

            var key = "Hello";
            var expected = "Hello!";

            //clean up old keys
            var deleteOperation = new DeleteOperation(key, GetVBucket(), converter, serializer);
            IOStrategy.Execute(deleteOperation);

            deleteOperation = new DeleteOperation(key + "!", GetVBucket(), converter, serializer);
            IOStrategy.Execute(deleteOperation);

            //create the key
            var set = new SetOperation<string>(key, "Hello", GetVBucket(), converter);
            var addResult = IOStrategy.Execute(set);
            Assert.IsTrue(addResult.Success);

            var append = new AppendOperation<string>(key, "!", serializer, GetVBucket(), converter);
            var result = IOStrategy.Execute(append);

            
            Assert.IsTrue(result.Success);
            Assert.AreEqual(string.Empty, result.Value);

            var get = new GetOperation<string>(key, GetVBucket(), converter, serializer);
            var getResult = IOStrategy.Execute(get);
            Assert.AreEqual(expected, getResult.Value);
        }
    }
}
