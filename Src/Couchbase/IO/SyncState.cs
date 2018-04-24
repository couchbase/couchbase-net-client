using System.Threading;

namespace Couchbase.IO
{
    /// <summary>
    /// Represents a synchronous Memcached operation.
    /// </summary>
    /// <seealso cref="Couchbase.IO.IState" />
    internal class SyncState : IState
    {
        public byte[] Response;
        public readonly AutoResetEvent SyncWait = new AutoResetEvent(false);

        /// <summary>
        /// Completes the specified Memcached response.
        /// </summary>
        /// <param name="response">The Memcached response packet.</param>
        /// <remarks>Exception is not used</remarks>
        public void Complete(byte[] response)
        {
            Response = response;
            SyncWait.Set();
        }

        public void CleanForReuse()
        {
            Response = null;
            SyncWait.Reset();
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
