using System;

namespace Couchbase.Core.Exceptions.KeyValue
{
    /// <summary>
    /// Thrown when the replica exists but is not available right now, for example during a rebalance.
    /// </summary>
    public class ReplicaIndexCurrentlyUnavailableException : KeyValueException
    {
        public ReplicaIndexCurrentlyUnavailableException()
        {
        }

        public ReplicaIndexCurrentlyUnavailableException(IErrorContext context) : base(context)
        {
        }

        public ReplicaIndexCurrentlyUnavailableException(IKeyValueErrorContext context) : base(context)
        {
        }

        public ReplicaIndexCurrentlyUnavailableException(string message) : base(message)
        {
        }

        public ReplicaIndexCurrentlyUnavailableException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2026 Couchbase, Inc.
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
