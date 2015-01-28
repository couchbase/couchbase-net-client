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
