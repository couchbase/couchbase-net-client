using System;
using Couchbase.IO;
using Couchbase.IO.Operations;
using NUnit.Framework;

namespace Couchbase.UnitTests.IO.Operations
{
    [TestFixture]
    public class OperationResultTests
    {
        [Test]
        [TestCase(ResponseStatus.Success)]
        [TestCase(ResponseStatus.Failure)]
        [TestCase(ResponseStatus.Success)]
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
        [TestCase(ResponseStatus.BucketNotConnected)]
        [TestCase(ResponseStatus.None)]
        public void SetException_Does_Not_Throw_Or_Set_Exception(ResponseStatus responseStatus)
        {
            var result = new OperationResult
            {
                Status = responseStatus
            };

            Assert.DoesNotThrow(()=>result.SetException());
            Assert.IsNull(result.Exception);
        }

        [Test]
        public void SetException_KeyNotFound_Sets_DocumentDoesNotExistException()
        {
            var result = new OperationResult
            {
                Status = ResponseStatus.KeyNotFound
            };

            result.SetException();
            Assert.IsInstanceOf<DocumentDoesNotExistException>(result.Exception);
        }

        [Test]
        public void SetException_KeyNotFound_Sets_CasMismatchException_When_Not_Add()
        {
            var result = new OperationResult
            {
                Status = ResponseStatus.KeyExists,
                OpCode = OperationCode.Get
            };

            result.SetException();
            Assert.IsInstanceOf<CasMismatchException>(result.Exception);
        }

        [Test]
        public void SetException_KeyNotFound_Sets_DocumentAlreadyExistsException()
        {
            var result = new OperationResult
            {
                Status = ResponseStatus.KeyExists,
                OpCode = OperationCode.Add
            };

            result.SetException();
            Assert.IsInstanceOf<DocumentAlreadyExistsException>(result.Exception);
        }

        [Test]
        [TestCase(ResponseStatus.ValueTooLarge, null)]
        [TestCase(ResponseStatus.InvalidArguments, null)]
        [TestCase(ResponseStatus.ItemNotStored, null)]
        [TestCase(ResponseStatus.IncrDecrOnNonNumericValue, null)]
        [TestCase(ResponseStatus.VBucketBelongsToAnotherServer, null)]
        [TestCase(ResponseStatus.AuthenticationError, null)]
        [TestCase(ResponseStatus.AuthenticationContinue, null)]
        [TestCase(ResponseStatus.InvalidRange, null)]
        [TestCase(ResponseStatus.UnknownCommand, null)]
        [TestCase(ResponseStatus.OutOfMemory, null)]
        [TestCase(ResponseStatus.NotSupported, null)]
        [TestCase(ResponseStatus.InternalError, null)]
        [TestCase(ResponseStatus.Busy, null)]
        [TestCase(ResponseStatus.TemporaryFailure, null)]
        [TestCase(ResponseStatus.ValueTooLarge, "LOCK_ERROR")]
        [TestCase(ResponseStatus.InvalidArguments, "LOCK_ERROR")]
        [TestCase(ResponseStatus.ItemNotStored, "LOCK_ERROR")]
        [TestCase(ResponseStatus.IncrDecrOnNonNumericValue, "LOCK_ERROR")]
        [TestCase(ResponseStatus.VBucketBelongsToAnotherServer, "LOCK_ERROR")]
        [TestCase(ResponseStatus.AuthenticationError, "LOCK_ERROR")]
        [TestCase(ResponseStatus.AuthenticationContinue, "LOCK_ERROR")]
        [TestCase(ResponseStatus.InvalidRange, "LOCK_ERROR")]
        [TestCase(ResponseStatus.UnknownCommand, "LOCK_ERROR")]
        [TestCase(ResponseStatus.OutOfMemory, "LOCK_ERROR")]
        [TestCase(ResponseStatus.NotSupported, "LOCK_ERROR")]
        [TestCase(ResponseStatus.InternalError, "LOCK_ERROR")]
        [TestCase(ResponseStatus.Busy, "LOCK_ERROR")]
        [TestCase(ResponseStatus.TemporaryFailure, "LOCK_ERROR")]
        public void SetException_Sets_TemporaryLockFailureException_Only_When_LOCK_ERROR(ResponseStatus responseStatus, string message)
        {
            var result = new OperationResult
            {
                Status = responseStatus,
                Message = message
            };

            result.SetException();
            if (message == null)
            {
               Assert.IsNull(result.Exception);
            }
            else
            {
                Assert.IsInstanceOf<TemporaryLockFailureException>(result.Exception);
            }
        }
    }
}

#region [ License information          ]
/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
