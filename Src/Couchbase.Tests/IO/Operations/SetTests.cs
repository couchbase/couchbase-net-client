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
    public class SetTests : OperationTestBase
    {
        [Test]
        public void When_Key_Does_Not_Exist_Set_Succeeds()
        {
            const string key = "Replace.When_Document_Exists_Replace_Succeeds";

            //delete the value if it exists
            var delete = new Delete(key, GetVBucket(), Converter, Serializer);
            IOStrategy.Execute(delete);

            var set = new Set<string>(key, "boo", GetVBucket(), Converter);
            var result = IOStrategy.Execute(set);
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void When_Key_Exists_Set_Succeeds()
        {
            const string key = "Replace.When_Document_Exists_Replace_Succeeds";

            //delete the value if it exists
            var delete = new Delete(key, GetVBucket(), Converter, Serializer);
            IOStrategy.Execute(delete);

            var add = new Add<string>(key, "foo", GetVBucket(), Converter, Serializer);
            Assert.IsTrue(IOStrategy.Execute(add).Success);

            var set = new Set<string>(key, "boo", GetVBucket(), Converter);
            var result = IOStrategy.Execute(set);
            Assert.IsTrue(result.Success);
        }
    }
}
