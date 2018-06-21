using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Annotations;

namespace Couchbase
{
    /// <summary>
    /// Thrown when the client cannot find a healthy server to execute an operation on. This
    /// could temporarily happen during a swap/failover/rebalance situation. The calling code
    /// could decide to retry the operation after handling this exception.
    /// </summary>
    public sealed class ServerUnavailableException : Exception
    {
        public ServerUnavailableException()
        {
        }

        public ServerUnavailableException(string message)
            : base(message)
        {
        }

        public ServerUnavailableException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

#if NET452
        public ServerUnavailableException([NotNull] SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
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
