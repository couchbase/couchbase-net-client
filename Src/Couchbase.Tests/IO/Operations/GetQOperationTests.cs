using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Operations
{
    [TestFixture]
    public class GetQOperationTests : OperationTestBase
    {
        [Test]
        public void When_Key_Exists_GetK_Returns_Value()
        {
            var key = "When_Key_Exists_GetQ_Returns_Value";

            //delete the value if it exists
            var delete = new Delete(key, GetVBucket(), new AutoByteConverter(), new DefaultTranscoder(new ManualByteConverter()), OperationLifespanTimeout);
            IOStrategy.Execute(delete);

            //Add the key
            var add = new Add<dynamic>(key, new { foo = "foo" }, GetVBucket(), new AutoByteConverter(), new DefaultTranscoder(new ManualByteConverter()), OperationLifespanTimeout);
            Assert.IsTrue(IOStrategy.Execute(add).Success);

            var getK = new GetQ<dynamic>(key, GetVBucket(), new AutoByteConverter(),
                new DefaultTranscoder(new AutoByteConverter()), OperationLifespanTimeout);

            var result = IOStrategy.Execute(getK);
            Assert.IsTrue(result.Success);

            var expected = new {foo = "foo"};
            Assert.AreEqual(result.Value.foo.Value, expected.foo);
        }

        [Test]
        public void Test_OperationResult_Returns_Defaults()
        {
            var op = new GetQ<string>("Key", GetVBucket(), new AutoByteConverter(),
                new DefaultTranscoder(new AutoByteConverter()), OperationLifespanTimeout);

            var result = op.GetResult();
            Assert.IsNull(result.Value);
            Assert.IsEmpty(result.Message);
        }

        [Test]
        public void When_Type_Is_String_DataFormat_String_Is_Used()
        {
            var key = "When_Type_Is_String_GetQ_Uses_DataFormat_String";

            //delete the value if it exists
            var delete = new Delete(key, GetVBucket(), new AutoByteConverter(), new DefaultTranscoder(new ManualByteConverter()), OperationLifespanTimeout);
            IOStrategy.Execute(delete);

            //Add the key
            var add = new Add<string>(key, "foo", GetVBucket(), new AutoByteConverter(), new DefaultTranscoder(new ManualByteConverter()), OperationLifespanTimeout);
            Assert.IsTrue(IOStrategy.Execute(add).Success);

            var getQ = new GetQ<string>(key, GetVBucket(), new AutoByteConverter(),
                new DefaultTranscoder(new AutoByteConverter()), OperationLifespanTimeout);

            getQ.CreateExtras();
            Assert.AreEqual(DataFormat.String, getQ.Format);

            var result = IOStrategy.Execute(getQ);
            Assert.IsTrue(result.Success);

            Assert.AreEqual(DataFormat.String, getQ.Format);
        }

        [Test]
        public void When_Type_Is_Object_DataFormat_Json_Is_Used()
        {
            var key = "When_Type_Is_Object_GetQ_Uses_DataFormat_Json";

            //delete the value if it exists
            var delete = new Delete(key, GetVBucket(), new AutoByteConverter(), new DefaultTranscoder(new ManualByteConverter()), OperationLifespanTimeout);
            IOStrategy.Execute(delete);

            //Add the key
            var add = new Add<dynamic>(key, new { foo = "foo" }, GetVBucket(), new AutoByteConverter(), new DefaultTranscoder(new ManualByteConverter()), OperationLifespanTimeout);
            Assert.IsTrue(IOStrategy.Execute(add).Success);

            var getQ = new GetQ<dynamic>(key, GetVBucket(), new AutoByteConverter(),
                new DefaultTranscoder(new AutoByteConverter()), OperationLifespanTimeout);

            getQ.CreateExtras();
            Assert.AreEqual(DataFormat.Json, getQ.Format);

            var result = IOStrategy.Execute(getQ);
            Assert.IsTrue(result.Success);

            Assert.AreEqual(DataFormat.Json, getQ.Format);
        }

        [Test]
        public void When_Type_Is_ByteArray_DataFormat_Binary_Is_Used()
        {
            var key = "When_Type_Is_Object_GetQ_Uses_DataFormat_Json";

            //delete the value if it exists
            var delete = new Delete(key, GetVBucket(), new AutoByteConverter(), new DefaultTranscoder(new ManualByteConverter()), OperationLifespanTimeout);
            IOStrategy.Execute(delete);

            //Add the key
            var add = new Add<byte[]>(key, new byte[] { 0x0 }, GetVBucket(), new AutoByteConverter(), new DefaultTranscoder(new ManualByteConverter()), OperationLifespanTimeout);
            Assert.IsTrue(IOStrategy.Execute(add).Success);

            var getQ = new GetQ<byte[]>(key, GetVBucket(), new AutoByteConverter(),
                new DefaultTranscoder(new AutoByteConverter()), OperationLifespanTimeout);

            getQ.CreateExtras();
            Assert.AreEqual(DataFormat.Binary, getQ.Format);

            var result = IOStrategy.Execute(getQ);
            Assert.IsTrue(result.Success);

            Assert.AreEqual(DataFormat.Binary, getQ.Format);
        }


        [Test]
        public void Test_Clone()
        {
            var operation = new GetQ<string>("key", GetVBucket(), Converter, Transcoder, OperationLifespanTimeout)
            {
                Cas = 1123
            };
            var cloned = operation.Clone();
            Assert.AreEqual(operation.CreationTime, cloned.CreationTime);
            Assert.AreEqual(operation.Cas, cloned.Cas);
            Assert.AreEqual(operation.VBucket.Index, cloned.VBucket.Index);
            Assert.AreEqual(operation.Key, cloned.Key);
            Assert.AreEqual(operation.Opaque, cloned.Opaque);
        }
    }
}
