using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Serializers;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Operations
{
    [TestFixture]
    public  class GetTests : OperationTestBase
    {
        [Test]
        public void When_Key_Exists_Get_Returns_Value()
        {
            var key = "When_Key_Exists_Get_Returns_Value";

            //delete the value if it exists
            var delete = new Delete(key, GetVBucket(), new AutoByteConverter(), new TypeSerializer(new ManualByteConverter()));
            IOStrategy.Execute(delete);

            //Add the key
            var add = new Add<dynamic>(key, new { foo = "foo" }, GetVBucket(), new AutoByteConverter(), new TypeSerializer(new ManualByteConverter()));
            Assert.IsTrue(IOStrategy.Execute(add).Success);
            
            var get = new Get<dynamic>(key, GetVBucket(), new AutoByteConverter(),
                new TypeSerializer(new AutoByteConverter()));

            var result = IOStrategy.Execute(get);
            Assert.IsTrue(result.Success);

            var expected = new {foo = "foo"};
            Assert.AreEqual(result.Value.foo.Value, expected.foo);
        }

        [Test]
        public void Test_OperationResult_Returns_Defaults()
        {
            var op = new Get<string>("Key", GetVBucket(), new AutoByteConverter(),
                new TypeSerializer(new AutoByteConverter()));

            var result = op.GetResult();
            Assert.IsNull(result.Value);
            Assert.IsEmpty(result.Message);
        }
    }
}
