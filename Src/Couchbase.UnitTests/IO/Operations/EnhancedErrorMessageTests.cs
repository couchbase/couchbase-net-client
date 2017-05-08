using System;
using System.IO;
using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.IO.Operations.Errors;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.IO.Operations
{
    [TestFixture]
    public class EnhancedErrorMessageTests
    {
        private static string GetOperationMessage(string responseBody, IOperation operation)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(responseBody);
                    writer.Flush();
                    stream.Position = 0;
                    operation.Data = stream;

                    return operation.GetMessage();
                }
            }
        }

        private static string GetOperationMessage(string context, string referece, IOperation operation)
        {
            var responseBody = JsonConvert.SerializeObject(new {error = new {context = context, @ref = referece}},
                Formatting.None);
            return GetOperationMessage(responseBody, operation);
        }

        [Test]
        public void Successful_Operation_Does_Not_Set_Result_Message()
        {
            var operation = new FakeOperation
            {
                Header = new OperationHeader {Status = ResponseStatus.Success},
                Flags = new Flags {DataFormat = DataFormat.Json}
            };

            var message = GetOperationMessage(null, null, operation);
            Assert.AreEqual(string.Empty, message);
        }

        [TestCase("test context", "test reference", "Context: test context, Ref #: test reference")]
        [TestCase("test context", "", "Context: test context, Ref #: <none>")]
        [TestCase("test context", null, "Context: test context, Ref #: <none>")]
        [TestCase("", "test reference", "Context: <none>, Ref #: test reference")]
        [TestCase(null, "test reference", "Context: <none>, Ref #: test reference")]
        public void Message_Should_Include_Additional_Context_And_Reference_Information(string context,
            string reference, string expected)
        {
            var operation = new FakeOperation
            {
                Header = new OperationHeader {Status = ResponseStatus.Failure, DataType = DataType.Json},
                ErrorCode = new ErrorCode
                {
                    Name = "failed",
                    Desc = "There was a problem",
                    Attrs = new[] {"unknown"}
                }
            };

            var message = GetOperationMessage(context, reference, operation);
            Assert.AreEqual(
                string.Format("{0} ({1})", operation.ErrorCode, expected),
                message
            );
        }

        [Test]
        public void Message_Should_Use_StatusCode_If_ErrorCode_Is_Empty()
        {
            var operation = new FakeOperation
            {
                Header = new OperationHeader {Status = ResponseStatus.Failure, DataType = DataType.Json}
            };

            var message = GetOperationMessage("test context", "test reference", operation);
            Assert.AreEqual("Status code: Failure [-3] (Context: test context, Ref #: test reference)", message);
        }

        [Test]
        public void Message_Does_Not_Include_Context_And_Reference_If_JSON_But_Is_Not_Set()
        {
            var operation = new FakeOperation
            {
                Header = new OperationHeader {Status = ResponseStatus.Failure},
                Flags = new Flags {DataFormat = DataFormat.Reserved}
            };

            var message = GetOperationMessage("test context", "test reference", operation);
            Assert.AreEqual("Status code: Failure [-3]", message);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("{ }")]
        [TestCase("{ \"error\": null }")]
        [TestCase("{ \"error\": { } }")]
        public void Message_Does_Not_Include_Context_And_Reference_If_ResponseBody_Is_Missing_Or_Invalid(
            string responseBody)
        {
            var operation = new FakeOperation
            {
                Header = new OperationHeader {Status = ResponseStatus.Failure},
                Flags = new Flags {DataFormat = DataFormat.Json}
            };

            var message = GetOperationMessage(responseBody, operation);
            Assert.AreEqual("Status code: Failure [-3]", message);
        }

        [Test]
        public void Existing_Exception_Has_Context_And_Reference_To_Data()
        {
            var exception = new Exception("test exception");

            var operation = new FakeOperation
            {
                Header = new OperationHeader {Status = ResponseStatus.Failure, DataType = DataType.Json},
                Exception = exception
            };

            var message = GetOperationMessage("test context", "test reference", operation);
            Assert.AreEqual("test exception (Context: test context, Ref #: test reference)", message);

            Assert.IsNotNull(operation.Exception);
            Assert.AreSame(exception, operation.Exception);
            Assert.AreEqual("test exception", operation.Exception.Message);
            Assert.AreEqual("test context", operation.Exception.Data["Context"]);
            Assert.AreEqual("test reference", operation.Exception.Data["Ref"]);
        }

        [Test]
        public void Creates_New_Exception_With_Context_And_Reference_Data_If_Not_Exception_Found()
        {
            var operation = new FakeOperation
            {
                Header = new OperationHeader {Status = ResponseStatus.Failure, DataType = DataType.Json}
            };

            var message = GetOperationMessage("test context", "test reference", operation);
            Assert.AreEqual("Status code: Failure [-3] (Context: test context, Ref #: test reference)", message);

            Assert.IsNotNull(operation.Exception);
            Assert.IsInstanceOf<CouchbaseKeyValueResponseException>(operation.Exception);
            Assert.AreEqual("Status code: Failure [-3] (Context: test context, Ref #: test reference)",
                operation.Exception.Message);
            Assert.AreEqual("test context", operation.Exception.Data["Context"]);
            Assert.AreEqual("test reference", operation.Exception.Data["Ref"]);
        }

        private class FakeOperation : OperationBase
        {
            public FakeOperation()
                : base("key", null, new DefaultTranscoder(), 0)
            {
            }

            public override OperationCode OperationCode
            {
                get { return OperationCode.NoOp; }
            }

            public new Flags Flags
            {
                get { return base.Flags; }
                set { base.Flags = value; }
            }

            public new ErrorCode ErrorCode
            {
                get { return base.ErrorCode; }
                set { base.ErrorCode = value; }
            }
        }
    }
}
