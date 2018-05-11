using System;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.IO.Operations.Errors;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.IO.Operations.Errors
{
    [TestFixture]
    public class KvErrorMapTests
    {
        //key exists
        [TestCase(OperationCode.Replace, ResponseStatus.Locked, ResponseStatus.KeyExists)]
        [TestCase(OperationCode.Delete, ResponseStatus.Locked, ResponseStatus.KeyExists)]
        [TestCase(OperationCode.Set, ResponseStatus.Locked, ResponseStatus.KeyExists)]
        //temp fail
        [TestCase(OperationCode.Get, ResponseStatus.Locked, ResponseStatus.TemporaryFailure)]
        [TestCase(OperationCode.GetL, ResponseStatus.Locked, ResponseStatus.TemporaryFailure)]
        [TestCase(OperationCode.GAT, ResponseStatus.Locked, ResponseStatus.TemporaryFailure)]
        [TestCase(OperationCode.GetK, ResponseStatus.Locked, ResponseStatus.TemporaryFailure)]
        [TestCase(OperationCode.GetKQ, ResponseStatus.Locked, ResponseStatus.TemporaryFailure)]
        [TestCase(OperationCode.Add, ResponseStatus.Locked, ResponseStatus.TemporaryFailure)]
        [TestCase(OperationCode.AddQ, ResponseStatus.Locked, ResponseStatus.TemporaryFailure)]
        [TestCase(OperationCode.Append, ResponseStatus.Locked, ResponseStatus.TemporaryFailure)]
        [TestCase(OperationCode.AppendQ, ResponseStatus.Locked, ResponseStatus.TemporaryFailure)]
        [TestCase(OperationCode.Decrement, ResponseStatus.Locked, ResponseStatus.TemporaryFailure)]
        [TestCase(OperationCode.DecrementQ, ResponseStatus.Locked, ResponseStatus.TemporaryFailure)]
        [TestCase(OperationCode.Increment, ResponseStatus.Locked, ResponseStatus.TemporaryFailure)]
        [TestCase(OperationCode.MultiLookup, ResponseStatus.Locked, ResponseStatus.TemporaryFailure)]
        [TestCase(OperationCode.Observe, ResponseStatus.Locked, ResponseStatus.TemporaryFailure)]
        [TestCase(OperationCode.ObserveSeqNo, ResponseStatus.Locked, ResponseStatus.TemporaryFailure)]
        [TestCase(OperationCode.ReplicaRead, ResponseStatus.Locked, ResponseStatus.TemporaryFailure)]
        [TestCase(OperationCode.SubArrayAddUnique, ResponseStatus.Locked, ResponseStatus.TemporaryFailure)]
        [TestCase(OperationCode.SubArrayInsert, ResponseStatus.Locked, ResponseStatus.TemporaryFailure)]
        [TestCase(OperationCode.SubArrayPushFirst, ResponseStatus.Locked, ResponseStatus.TemporaryFailure)]
        [TestCase(OperationCode.SubArrayPushLast, ResponseStatus.Locked, ResponseStatus.TemporaryFailure)]
        [TestCase(OperationCode.SubCounter, ResponseStatus.Locked, ResponseStatus.TemporaryFailure)]
        public void GetResponseStatus_Translate_Locked_Status(OperationCode operationCode, ResponseStatus status, ResponseStatus translatedStatus)
        {
            var operation = new FakeOperation(operationCode, status);
            var actual = operation.GetResponseStatus();

            Assert.AreEqual(translatedStatus, actual);
        }

        //key exists
        [TestCase(OperationCode.Replace, ResponseStatus.Locked, ResponseStatus.KeyExists, typeof(CasMismatchException))]
        [TestCase(OperationCode.Delete, ResponseStatus.Locked, ResponseStatus.KeyExists, typeof(CasMismatchException))]
        [TestCase(OperationCode.Set, ResponseStatus.Locked, ResponseStatus.KeyExists, typeof(CasMismatchException))]
        //temp_fail
        [TestCase(OperationCode.Get, ResponseStatus.Locked, ResponseStatus.TemporaryFailure, typeof(TemporaryLockFailureException))]
        [TestCase(OperationCode.GetL, ResponseStatus.Locked, ResponseStatus.TemporaryFailure, typeof(TemporaryLockFailureException))]
        [TestCase(OperationCode.GAT, ResponseStatus.Locked, ResponseStatus.TemporaryFailure, typeof(TemporaryLockFailureException))]
        [TestCase(OperationCode.GetK, ResponseStatus.Locked, ResponseStatus.TemporaryFailure, typeof(TemporaryLockFailureException))]
        [TestCase(OperationCode.GetKQ, ResponseStatus.Locked, ResponseStatus.TemporaryFailure, typeof(TemporaryLockFailureException))]
        [TestCase(OperationCode.Add, ResponseStatus.Locked, ResponseStatus.TemporaryFailure, typeof(TemporaryLockFailureException))]
        [TestCase(OperationCode.AddQ, ResponseStatus.Locked, ResponseStatus.TemporaryFailure, typeof(TemporaryLockFailureException))]
        [TestCase(OperationCode.Append, ResponseStatus.Locked, ResponseStatus.TemporaryFailure, typeof(TemporaryLockFailureException))]
        [TestCase(OperationCode.AppendQ, ResponseStatus.Locked, ResponseStatus.TemporaryFailure, typeof(TemporaryLockFailureException))]
        [TestCase(OperationCode.Decrement, ResponseStatus.Locked, ResponseStatus.TemporaryFailure, typeof(TemporaryLockFailureException))]
        [TestCase(OperationCode.DecrementQ, ResponseStatus.Locked, ResponseStatus.TemporaryFailure, typeof(TemporaryLockFailureException))]
        [TestCase(OperationCode.Increment, ResponseStatus.Locked, ResponseStatus.TemporaryFailure, typeof(TemporaryLockFailureException))]
        [TestCase(OperationCode.MultiLookup, ResponseStatus.Locked, ResponseStatus.TemporaryFailure, typeof(TemporaryLockFailureException))]
        [TestCase(OperationCode.Observe, ResponseStatus.Locked, ResponseStatus.TemporaryFailure, typeof(TemporaryLockFailureException))]
        [TestCase(OperationCode.ObserveSeqNo, ResponseStatus.Locked, ResponseStatus.TemporaryFailure, typeof(TemporaryLockFailureException))]
        [TestCase(OperationCode.ReplicaRead, ResponseStatus.Locked, ResponseStatus.TemporaryFailure, typeof(TemporaryLockFailureException))]
        [TestCase(OperationCode.SubArrayAddUnique, ResponseStatus.Locked, ResponseStatus.TemporaryFailure, typeof(TemporaryLockFailureException))]
        [TestCase(OperationCode.SubArrayInsert, ResponseStatus.Locked, ResponseStatus.TemporaryFailure, typeof(TemporaryLockFailureException))]
        [TestCase(OperationCode.SubArrayPushFirst, ResponseStatus.Locked, ResponseStatus.TemporaryFailure, typeof(TemporaryLockFailureException))]
        [TestCase(OperationCode.SubArrayPushLast, ResponseStatus.Locked, ResponseStatus.TemporaryFailure, typeof(TemporaryLockFailureException))]
        [TestCase(OperationCode.SubCounter, ResponseStatus.Locked, ResponseStatus.TemporaryFailure, typeof(TemporaryLockFailureException))]
        public void Test_SetException(OperationCode operationCode, ResponseStatus status, ResponseStatus translatedStatus, Type exceptionType)
        {
            var code = ((short)status).ToString("X").ToLower();
            var json = ResourceHelper.ReadResource(@"Data\kv-error-map-v5.5.0.json");
            var errorMap = JsonConvert.DeserializeObject<ErrorMap>(json);
            var errorCode = errorMap.Errors[code];

            var operation = new FakeOperation(operationCode, status, errorCode);
            var result = operation.GetResult();
            ((OperationResult)result).SetException();

            Assert.AreEqual(translatedStatus, result.Status);
            Assert.IsInstanceOf(exceptionType, result.Exception);
        }

        private class FakeOperation : OperationBase
        {
            private readonly OperationCode _operationCode;

            public FakeOperation(OperationCode operationCode, ResponseStatus status, ErrorCode errorCode = null)
                : base("hello", null, new DefaultTranscoder(), 0)
            {
                _operationCode = operationCode;
                Header = new OperationHeader
                {
                    Status = status,
                    OperationCode = _operationCode
                };
                ErrorCode = errorCode;
            }

            public override OperationCode OperationCode => _operationCode;
        }
    }
}
