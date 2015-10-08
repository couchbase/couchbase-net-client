using System;

namespace Couchbase
{
    /// <summary>
    /// Default interface for all operation return types.
    /// </summary>
    public interface IResult
    {
        /// <summary>
        /// Returns true if the operation was succesful.
        /// </summary>
        /// <remarks>If Success is false, use the Message property to help determine the reason.</remarks>
        bool Success { get; }

        /// <summary>
        /// If the operation wasn't succesful, a message indicating why it was not succesful.
        /// </summary>
        string Message { get; }

        /// <summary>
        /// If Success is false and an exception has been caught internally, this field will contain the exception.
        /// </summary>
        Exception Exception { get; }

        /// <summary>
        /// If the response indicates the request is retryable, returns true.
        /// </summary>
        /// <returns></returns>
        /// <remarks>Intended for internal use only.</remarks>
        bool ShouldRetry();
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
