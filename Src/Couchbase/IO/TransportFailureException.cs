using System;

namespace Couchbase.IO
{
    /// <summary>
    /// Returned when an operation is attempted on a connection which has been closed by the remote host or
    /// for some other reason and before the client has determined that the Couchbase base node is either offline
    /// or otherwise unavailable. It should be considered a temporary error in that the client will try to reconnect
    /// and try again on the new connection.
    /// </summary>
    /// <seealso cref="System.Exception" />
    public class TransportFailureException : Exception
    {
        public TransportFailureException()
        {
        }

        public TransportFailureException(string message)
            : base(message)
        {
        }

        public TransportFailureException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
