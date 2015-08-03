using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.IO.Converters;
using Couchbase.IO.Utils;

namespace Couchbase.IO
{
    /// <summary>
    /// Represents an asynchronous Memcached request in flight.
    /// </summary>
    internal class AsyncState : IState
    {
        public Func<SocketAsyncState, Task> Callback { get; set; }
        public IByteConverter Converter { get; set; }
        public uint Id { get; set; }
        public Timer Timer;

        /// <summary>
        /// Cancels the current Memcached request that is in-flight.
        /// </summary>
        public void Cancel()
        {
            Timer.Dispose();

            var response = new byte[24];
            Converter.FromUInt32(Id, response, HeaderIndexFor.Opaque);
            Callback(new SocketAsyncState
            {
                Data = new MemoryStream(response),
                Opaque = Id,
                Status = ResponseStatus.ClientFailure
            });
        }

        /// <summary>
        /// Completes the specified Memcached response.
        /// </summary>
        /// <param name="response">The Memcached response packet.</param>
        public void Complete(byte[] response)
        {
            Timer.Dispose();

            //this means the request never completed
            if (response == null)
            {
                response = new byte[24];
                Converter.FromUInt32(Id, response, HeaderIndexFor.Opaque);
            }

            //somewhat of hack for backwards compatibility
            Callback(new SocketAsyncState
            {
                Data = new MemoryStream(response),
                Opaque = Id
            });
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
