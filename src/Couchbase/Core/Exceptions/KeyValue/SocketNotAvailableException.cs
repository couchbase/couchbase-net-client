using Couchbase.Core.Retry;

namespace Couchbase.Core.Exceptions.KeyValue
{
    /// <summary>
    /// Thrown when a socket is temporarily unavailable. This exception is caught and will force a retry.
    /// </summary>
    public class SocketNotAvailableException : KeyValueException, IRetryable
    {
        public SocketNotAvailableException(string message) : base(message)
        {
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
