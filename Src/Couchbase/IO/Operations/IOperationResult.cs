namespace Couchbase.IO.Operations
{
    /// <summary>
    /// The result of an operation.
    /// </summary>
    /// <remarks>If Success is false, use the Message property to help determine the reason.</remarks>
    /// <typeparam name="T"></typeparam>
    public interface IOperationResult<out T>
    {
        /// <summary>
        /// True if the operation succeeded.
        /// </summary>
        bool Success { get; }

        /// <summary>
        /// The value of the key retrieved from Couchbase Server.
        /// </summary>
        T Value { get; }

        /// <summary>
        /// If Success is false, the reasom why the operation failed.
        /// </summary>
        string Message { get; }

        /// <summary>
        /// The status returned from the Couchbase Server after an operation.
        /// </summary>
        /// <remarks><see cref="ResponseStatus.Success"/> will be returned if <see cref="Success"/>
        /// is true, otherwise <see cref="Success"/> will be false. If <see cref="ResponseStatus.ClientFailure"/> is
        /// returned, then the operation failed before being sent to the Couchbase Server.</remarks>
        ResponseStatus Status { get; }

        /// <summary>
        /// The Check-and-swap value for a given key or document.
        /// </summary>
        ulong Cas { get; }

        /// <summary>
        /// Indicates that the given durability requirements were met by the operation request.
        /// </summary>
        Durability Durability { get; set; }
    }
}

#region [ License information          ]

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

#endregion [ License information          ]