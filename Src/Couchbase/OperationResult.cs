using System.Net.Sockets;
using Couchbase.Core.Buckets;
using Couchbase.IO;
using Couchbase.IO.Operations;

namespace Couchbase
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
        public ulong Cas { get; internal set; }

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
                case ResponseStatus.VBucketBelongsToAnotherServer:
                case ResponseStatus.NodeUnavailable:
                    return true;
                case ResponseStatus.ClientFailure:
                    return IsClientFailureRetriable();
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
                    return false;
                default:
                    return false;
            }
        }

        bool IsClientFailureRetriable()
        {
            if (Exception is SocketException)
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
