using System;
using System.Net.Sockets;
using Couchbase.IO;
using Couchbase.IO.Operations;
using NUnit.Framework;

namespace Couchbase.UnitTests
{
    [TestFixture]
    public class OperationResultTests
    {
        [Test]
        public void OperationResult_ToString_TokenIsNull()
        {
            var result = new OperationResult
            {
                Cas = 10202020202
            };

            var expected = "{\"id\":null,\"cas\":10202020202,\"token\":null}";
            Assert.AreEqual(expected, result.ToString().Replace(" ", ""));
        }

        [Test]
        public void OperationResult_ToString_IdNotNull()
        {
            var result = new OperationResult
            {
                Cas = 10202020202,
                Id = "foo"
            };

            var expected = "{\"id\":\"foo\",\"cas\":10202020202,\"token\":null}";
            Assert.AreEqual(expected, result.ToString());
        }

        [Test]
        public void OperationResult_ToString_ContentNotNull()
        {
            var result = new OperationResult<dynamic>
            {
                Cas = 10202020202,
                Id = "foo",
                Value = new { Name="ted", Age=10}
            };

            var expected = "{\"id\":\"foo\",\"cas\":10202020202,\"token\":null,\"content\":\"{\\\"Name\\\":\\\"ted\\\",\\\"Age\\\":10}\"}";
            Assert.AreEqual(expected, result.ToString());
        }


        [Test]
        public void DocumentResult_ToString_ContentNotNull()
        {
            var result = new DocumentResult<dynamic>(new OperationResult<dynamic>
            {
                Cas = 10202020202,
                Id = "foo",
                Value = new { Name = "ted", Age = 10 }
            });

            var expected = "{\"id\":\"foo\",\"cas\":10202020202,\"token\":null,\"content\":\"{\\\"name\\\":\\\"ted\\\",\\\"age\\\":10}\"}";
            Assert.IsNotNull(expected, result.ToString());
        }

        [Test]
        public void Document_ToString_ContentNotNull()
        {
            var result = new DocumentResult<dynamic>(new OperationResult<dynamic>
            {
                Cas = 10202020202,
                Id = "foo",
                Value = new { Name = "ted", Age = 10 }
            });

            var expected = "{\"id\":\"foo\",\"cas\":10202020202,\"token\":null,\"content\":\"{\\\"Name\\\":\\\"ted\\\",\\\"Age\\\":10}\"}";
            Assert.AreEqual(expected, result.Document.ToString());
        }

        [TestCase(ResponseStatus.TransportFailure, true)]
        [TestCase(ResponseStatus.VBucketBelongsToAnotherServer, true)]
        [TestCase(ResponseStatus.Failure, true)]
        [TestCase(ResponseStatus.NodeUnavailable, false)]
        [TestCase(ResponseStatus.Success, false)]
        [TestCase(ResponseStatus.KeyNotFound, false)]
        [TestCase(ResponseStatus.KeyExists, false)]
        [TestCase(ResponseStatus.ValueTooLarge, false)]
        [TestCase(ResponseStatus.InvalidArguments, false)]
        [TestCase(ResponseStatus.ItemNotStored, false)]
        [TestCase(ResponseStatus.IncrDecrOnNonNumericValue, false)]
        [TestCase(ResponseStatus.AuthenticationError, false)]
        [TestCase(ResponseStatus.AuthenticationContinue, false)]
        [TestCase(ResponseStatus.InvalidRange, false)]
        [TestCase(ResponseStatus.UnknownCommand, false)]
        [TestCase(ResponseStatus.OutOfMemory, false)]
        [TestCase(ResponseStatus.NotSupported, false)]
        [TestCase(ResponseStatus.InternalError, false)]
        [TestCase(ResponseStatus.Busy, false)]
        [TestCase(ResponseStatus.OperationTimeout, false)]
        [TestCase(ResponseStatus.TemporaryFailure, false)]
        [TestCase(ResponseStatus.None, false)] // default case
        public void ShouldRetry_Should_Return_ExpectedValue(ResponseStatus status, bool shouldRetry)
        {
            var operationResult = new OperationResult
            {
                Success = false,
                Status = status
            };

            Assert.AreEqual(shouldRetry, operationResult.ShouldRetry());
        }

        [Test]
        public void ShouldRetry_Should_Return_True_When_Exception_Is_SocketExeption()
        {
            var operationResult = new OperationResult
            {
                Success = false,
                Status = ResponseStatus.ClientFailure,
                Exception = new SocketException()
            };

            Assert.IsTrue(operationResult.ShouldRetry());
        }

        [Test]
        public void ShouldRetry_Should_Return_True_When_Exception_Is_TimeoutExeption()
        {
            var operationResult = new OperationResult
            {
                Success = false,
                Status = ResponseStatus.ClientFailure,
                Exception = new TimeoutException()
            };

            Assert.IsTrue(operationResult.ShouldRetry());
        }

        [Test]
        public void ShouldRetry_Should_Return_False_When_Generic_Exception()
        {
            var operationResult = new OperationResult
            {
                Success = false,
                Status = ResponseStatus.ClientFailure,
                Exception = new Exception()
            };

            Assert.IsFalse(operationResult.ShouldRetry());
        }

        [Test]
        public void SetException_Should_Set_DocumentDoesNotExistException_When_Status_Is_KeyNotFound()
        {
            var operationResult = new OperationResult
            {
                Success = false,
                Status = ResponseStatus.KeyNotFound
            };

            operationResult.SetException();

            Assert.IsInstanceOf<DocumentDoesNotExistException>(operationResult.Exception);
        }

        [Test]
        public void SetException_Should_Set_DocumentAlreadyExistsException_When_Status_Is_KeyExists_And_OpCode_Is_Add()
        {
            var operationResult = new OperationResult
            {
                Success = false,
                Status = ResponseStatus.KeyExists,
                OpCode = OperationCode.Add
            };

            operationResult.SetException();

            Assert.IsInstanceOf<DocumentAlreadyExistsException>(operationResult.Exception);
        }

        [Test]
        public void SetException_Should_Set_CasMismatchException_When_Status_Is_KeyExists_And_OpCode_Is_Not_Add()
        {
            foreach (var value in Enum.GetValues(typeof(OperationCode)))
            {
                var opCode = (OperationCode) value;
                if (opCode == OperationCode.Add)
                {
                    break;
                }

                var operationResult = new OperationResult
                {
                    Success = false,
                    Status = ResponseStatus.KeyExists,
                    OpCode = opCode
                };

                operationResult.SetException();
                Assert.IsInstanceOf<CasMismatchException>(operationResult.Exception);
            }
        }

        [Test]
        public void SetException_Should_Set_CasMismatchException_When_Status_Is_DocumentMutationDetected()
        {
            var operationResult = new OperationResult
            {
                Success = false,
                Status = ResponseStatus.DocumentMutationDetected
            };

            operationResult.SetException();
            Assert.IsInstanceOf<CasMismatchException>(operationResult.Exception);
        }

        [TestCase(ResponseStatus.ValueTooLarge)]
        [TestCase(ResponseStatus.InvalidArguments)]
        [TestCase(ResponseStatus.ItemNotStored)]
        [TestCase(ResponseStatus.IncrDecrOnNonNumericValue)]
        [TestCase(ResponseStatus.VBucketBelongsToAnotherServer)]
        [TestCase(ResponseStatus.AuthenticationError)]
        [TestCase(ResponseStatus.AuthenticationContinue)]
        [TestCase(ResponseStatus.InvalidRange)]
        [TestCase(ResponseStatus.UnknownCommand)]
        [TestCase(ResponseStatus.OutOfMemory)]
        [TestCase(ResponseStatus.NotSupported)]
        [TestCase(ResponseStatus.InternalError)]
        [TestCase(ResponseStatus.Busy)]
        [TestCase(ResponseStatus.TemporaryFailure)]
        public void SetException_Should_Set_TemporaryLockFailureException_When_Message_Contatains_LOCKED(ResponseStatus status)
        {
            var operationResult = new OperationResult
            {
                Success = false,
                Status = status,
                Message = "error - LOCK_ERROR"
            };

            operationResult.SetException();
            Assert.IsInstanceOf<TemporaryLockFailureException>(operationResult.Exception);
        }

        [TestCase(ResponseStatus.None)]
        [TestCase(ResponseStatus.Success)]
        [TestCase(ResponseStatus.ClientFailure)]
        [TestCase(ResponseStatus.OperationTimeout)]
        [TestCase(ResponseStatus.NoReplicasFound)]
        [TestCase(ResponseStatus.NodeUnavailable)]
        [TestCase(ResponseStatus.TransportFailure)]
        [TestCase(ResponseStatus.DocumentMutationLost)]
        [TestCase(ResponseStatus.SubDocPathNotFound)]
        [TestCase(ResponseStatus.SubDocPathMismatch)]
        [TestCase(ResponseStatus.SubDocPathInvalid)]
        [TestCase(ResponseStatus.SubDocPathTooBig)]
        [TestCase(ResponseStatus.SubDocDocTooDeep)]
        [TestCase(ResponseStatus.SubDocCannotInsert)]
        [TestCase(ResponseStatus.SubDocDocNotJson)]
        [TestCase(ResponseStatus.SubDocNumRange)]
        [TestCase(ResponseStatus.SubDocDeltaRange)]
        [TestCase(ResponseStatus.SubDocPathExists)]
        [TestCase(ResponseStatus.SubDocValueTooDeep)]
        [TestCase(ResponseStatus.SubDocInvalidCombo)]
        [TestCase(ResponseStatus.SubDocMultiPathFailure)]
        public void SetException_Should_Not_Set_An_Exception_For_Given_Status(ResponseStatus status)
        {
            var operationResult = new OperationResult
            {
                Success = false,
                Status = status
            };

            operationResult.SetException();
            Assert.IsNull(operationResult.Exception);
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion
