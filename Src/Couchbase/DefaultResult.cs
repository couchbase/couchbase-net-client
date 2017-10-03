using System;

namespace Couchbase
{
    /// <summary>
    /// Basic operation return value
    /// </summary>
    public class DefaultResult : IResult
    {
        public DefaultResult()
        {
        }
        public DefaultResult(bool success, string message, Exception exception)
        {
            Success = success;
            Message = message;
            Exception = exception;
        }

        /// <summary>
        /// Returns true if the operation was succesful.
        /// </summary>
        /// <remarks>If Success is false, use the Message property to help determine the reason.</remarks>
        public bool Success { get; internal set; }

        /// <summary>
        /// If the operation wasn't succesful, a message indicating why it was not succesful.
        /// </summary>
        public string Message { get; internal set; }

        /// <summary>
        /// If Success is false and an exception has been caught internally, this field will contain the exception.
        /// </summary>
        public Exception Exception { get; internal set; }

        public bool ShouldRetry()
        {
            throw new NotImplementedException();
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
