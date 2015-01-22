using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Operations
{
    [TestFixture]
    public class GetKOperationTests : OperationTestBase
    {
        [Test]
        public void When_Key_Exists_GetK_Returns_Value()
        {
            var key = "When_Key_Exists_GetK_Returns_Value";

            //delete the value if it exists
            var delete = new Delete(key, GetVBucket(), new AutoByteConverter(), new DefaultTranscoder(new ManualByteConverter()));
            IOStrategy.Execute(delete);

            //Add the key
            var add = new Add<dynamic>(key, new { foo = "foo" }, GetVBucket(), new AutoByteConverter(), new DefaultTranscoder(new ManualByteConverter()));
            Assert.IsTrue(IOStrategy.Execute(add).Success);

            var getK = new GetK<dynamic>(key, GetVBucket(), new AutoByteConverter(),
                new DefaultTranscoder(new AutoByteConverter()));

            var result = IOStrategy.Execute(getK);
            Assert.IsTrue(result.Success);

            var expected = new {foo = "foo"};
            Assert.AreEqual(result.Value.foo.Value, expected.foo);
        }

        [Test]
        public void Test_OperationResult_Returns_Defaults()
        {
            var op = new GetK<string>("Key", GetVBucket(), new AutoByteConverter(),
                new DefaultTranscoder(new AutoByteConverter()));

            var result = op.GetResult();
            Assert.IsNull(result.Value);
            Assert.IsEmpty(result.Message);
        }

        [Test]
        public void When_Type_Is_String_DataFormat_String_Is_Used()
        {
            var key = "When_Type_Is_String_DataFormat_String_Is_Used";

            //delete the value if it exists
            var delete = new Delete(key, GetVBucket(), new AutoByteConverter(), new DefaultTranscoder(new ManualByteConverter()));
            IOStrategy.Execute(delete);

            //Add the key
            var add = new Add<string>(key, "foo", GetVBucket(), new AutoByteConverter(), new DefaultTranscoder(new ManualByteConverter()));
            Assert.IsTrue(IOStrategy.Execute(add).Success);

            var getK = new GetK<string>(key, GetVBucket(), new AutoByteConverter(),
                new DefaultTranscoder(new AutoByteConverter()));

            getK.CreateExtras();
            Assert.AreEqual(DataFormat.String, getK.Format);

            var result = IOStrategy.Execute(getK);
            Assert.IsTrue(result.Success);

            Assert.AreEqual(DataFormat.String, getK.Format);
        }

        [Test]
        public void When_Type_Is_Object_DataFormat_Json_Is_Used()
        {
            var key = "When_Type_Is_Object_GetK_Uses_DataFormat_Json";

            //delete the value if it exists
            var delete = new Delete(key, GetVBucket(), new AutoByteConverter(), new DefaultTranscoder(new ManualByteConverter()));
            IOStrategy.Execute(delete);

            //Add the key
            var add = new Add<dynamic>(key, new { foo = "foo" }, GetVBucket(), new AutoByteConverter(), new DefaultTranscoder(new ManualByteConverter()));
            Assert.IsTrue(IOStrategy.Execute(add).Success);

            var getK = new GetK<dynamic>(key, GetVBucket(), new AutoByteConverter(),
                new DefaultTranscoder(new AutoByteConverter()));

            getK.CreateExtras();
            Assert.AreEqual(DataFormat.Json, getK.Format);

            var result = IOStrategy.Execute(getK);
            Assert.IsTrue(result.Success);

            Assert.AreEqual(DataFormat.Json, getK.Format);
        }

        [Test]
        public void When_Type_Is_ByteArray_DataFormat_Binary_Is_Used()
        {
            var key = "When_Type_Is_Object_GetK_Uses_DataFormat_Json";

            //delete the value if it exists
            var delete = new Delete(key, GetVBucket(), new AutoByteConverter(), new DefaultTranscoder(new ManualByteConverter()));
            IOStrategy.Execute(delete);

            //Add the key
            var add = new Add<byte[]>(key, new byte[]{0x0}, GetVBucket(), new AutoByteConverter(), new DefaultTranscoder(new ManualByteConverter()));
            Assert.IsTrue(IOStrategy.Execute(add).Success);

            var getK = new GetK<byte[]>(key, GetVBucket(), new AutoByteConverter(),
                new DefaultTranscoder(new AutoByteConverter()));

            getK.CreateExtras();
            Assert.AreEqual(DataFormat.Binary, getK.Format);

            var result = IOStrategy.Execute(getK);
            Assert.IsTrue(result.Success);

            Assert.AreEqual(DataFormat.Binary, getK.Format);
        }


        [Test]
        public void Test_Clone()
        {
            var operation = new GetK<string>("key", GetVBucket(), Converter, Transcoder)
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
