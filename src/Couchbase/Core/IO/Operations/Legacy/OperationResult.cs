using System;
using System.Net.Sockets;
using Couchbase.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Couchbase.Core.IO.Operations.Legacy
{
    /// <summary>
    /// The result of an operation.
    /// </summary>
    /// <remarks>If Success is false, use the Message property to help determine the reason.</remarks>
    public class OperationResult : IOperationResult
    {
        /// <summary>
        /// True if the operation succeeded.
        /// </summary>
        /// <remarks>If Success is false, use the Message property to help determine the reason.</remarks>
        public bool Success { get; internal set; }

        /// <summary>
        /// If Success is false, the reason why the operation failed.
        /// </summary>
        public string Message { get; internal set; }

        /// <summary>
        /// Gets the mutation token for the operation if enhanced durability is enabled.
        /// </summary>
        /// <value>
        /// The mutation token.
        /// </value>
        public MutationToken Token { get; internal set; }

        /// <summary>
        /// The 'Check and Set' or 'CAS' value for enforcing optimistic concurrency.
        /// </summary>
        public ulong Cas { get; set; }

        public TimeSpan? Expiration { get; }

        /// <summary>
        /// The status returned from the Couchbase Server after an operation.
        /// </summary>
        /// <remarks><see cref="ResponseStatus.Success"/> will be returned if <see cref="Success"/>
        /// is true, otherwise <see cref="Success"/> will be false. If <see cref="ResponseStatus.ClientFailure"/> is
        /// returned, then the operation failed before being sent to the Couchbase Server.</remarks>
        public ResponseStatus Status { get; internal set; }

        /// <summary>
        /// The level of durability that the operation achieved
        /// </summary>
        public Durability Durability { get; set; }

        /// <summary>
        /// If Success is false and an exception has been caught internally, this field will contain the exception.
        /// </summary>
        public System.Exception Exception { get; set; }

        public bool ShouldRetry()
        {
            switch (Status)
            {
                case ResponseStatus.TransportFailure:
                case ResponseStatus.VBucketBelongsToAnotherServer:
                    return true;
                case ResponseStatus.ClientFailure:
                    return IsClientFailureRetriable();
                case ResponseStatus.NodeUnavailable:
                case ResponseStatus.Success:
                case ResponseStatus.KeyNotFound:
                case ResponseStatus.KeyExists:
                case ResponseStatus.ValueTooLarge:
                case ResponseStatus.InvalidArguments:
                case ResponseStatus.ItemNotStored:
                case ResponseStatus.IncrDecrOnNonNumericValue:
                case ResponseStatus.AuthenticationError:
                case ResponseStatus.AuthenticationContinue:
                case ResponseStatus.InvalidRange:
                case ResponseStatus.UnknownCommand:
                case ResponseStatus.OutOfMemory:
                case ResponseStatus.NotSupported:
                case ResponseStatus.InternalError:
                case ResponseStatus.Busy:
                case ResponseStatus.OperationTimeout:
                case ResponseStatus.TemporaryFailure:
                case ResponseStatus.BucketNotConnected:
                    return false;
                case ResponseStatus.Failure: // used for server retry straegies
                    return true;
                default:
                    return false;
            }
        }

        bool IsClientFailureRetriable()
        {
            if (Exception is SocketException || Exception is TimeoutException)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if the server responded with a Not My Vbucket.
        /// </summary>
        /// <returns>Returns true if <see cref="ResponseStatus"/> is a VBucketBelongsToAnotherServer.</returns>
        public bool IsNmv()
        {
            return Status == ResponseStatus.VBucketBelongsToAnotherServer;
        }

        /// <summary>
        /// Gets the id or key for the document.
        /// </summary>
        /// <value>
        /// The identifier.
        /// </value>
        public string Id { get; internal set; }

        /// <summary>
        /// Gets the <see cref="OpCode"/> for the operation.
        /// </summary>
        /// <value>
        /// The op code.
        /// </value>
        public OpCode OpCode { get; internal set; }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return new JObject(
                new JProperty("id", Id),
                new JProperty("cas", Cas),
                new JProperty("token", Token != null ? Token.ToString() : null)).
                ToString(Formatting.None);
        }

        /// <summary>
        /// Sets the <see cref="Exception"/> based upon the <see cref="Status"/> returned by the server.
        /// </summary>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        internal void SetException()
        {
            switch (Status)
            {
                case ResponseStatus.None:
                case ResponseStatus.Success:
                case ResponseStatus.BucketNotConnected:
                case ResponseStatus.Failure:
                    break;
                case ResponseStatus.KeyNotFound:
                    //Exception = new DocumentDoesNotExistException(ExceptionUtil.DocumentNotFoundMsg.WithParams(Id));
                    break;
                case ResponseStatus.KeyExists:
                    if (OpCode != OpCode.Add)
                    {
                        Exception = new KeyValueException(ExceptionUtil.CasMismatchMsg.WithParams(Id));
                    }
                    else
                    {
                      //  Exception = new DocumentAlreadyExistsException(ExceptionUtil.DocumentExistsMsg.WithParams(Id));
                    }
                    break;
                case ResponseStatus.ValueTooLarge:
                case ResponseStatus.InvalidArguments:
                case ResponseStatus.ItemNotStored:
                case ResponseStatus.IncrDecrOnNonNumericValue:
                case ResponseStatus.VBucketBelongsToAnotherServer:
                case ResponseStatus.AuthenticationError:
                case ResponseStatus.AuthenticationContinue:
                case ResponseStatus.InvalidRange:
                case ResponseStatus.UnknownCommand:
                case ResponseStatus.OutOfMemory:
                case ResponseStatus.NotSupported:
                case ResponseStatus.InternalError:
                case ResponseStatus.Busy:
                case ResponseStatus.TemporaryFailure:
                    if (Message != null && (Message.Contains("LOCK_ERROR") || Message.Contains("LOCKED")))
                    {
                        //Exception = new TemporaryLockFailureException(ExceptionUtil.TemporaryLockErrorMsg.WithParams(Id));
                    }
                    break;
                case ResponseStatus.ClientFailure:
                case ResponseStatus.OperationTimeout:
                case ResponseStatus.NoReplicasFound:
                case ResponseStatus.NodeUnavailable:
                case ResponseStatus.TransportFailure:
                case ResponseStatus.DocumentMutationLost:
                    break;
                case ResponseStatus.DocumentMutationDetected:
                    Exception = new KeyValueException(ExceptionUtil.CasMismatchMsg.WithParams(Id));
                    break;
                case ResponseStatus.SubDocPathNotFound:
                case ResponseStatus.SubDocPathMismatch:
                case ResponseStatus.SubDocPathInvalid:
                case ResponseStatus.SubDocPathTooBig:
                case ResponseStatus.SubDocDocTooDeep:
                case ResponseStatus.SubDocCannotInsert:
                case ResponseStatus.SubDocDocNotJson:
                case ResponseStatus.SubDocNumRange:
                case ResponseStatus.SubDocDeltaRange:
                case ResponseStatus.SubDocPathExists:
                case ResponseStatus.SubDocValueTooDeep:
                case ResponseStatus.SubDocInvalidCombo:
                case ResponseStatus.SubDocMultiPathFailure:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(ExceptionUtil.InvalidOpCodeMsg.WithParams(OpCode, Id, Status));
            }
        }
    }
}

#region [ License information ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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
