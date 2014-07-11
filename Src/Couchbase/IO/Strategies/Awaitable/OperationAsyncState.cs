using System.IO;
using System.Net.Security;
using Couchbase.IO.Operations;

namespace Couchbase.IO.Strategies.Awaitable
{
    /// <summary>
    /// Maintains state while an asynchronous operation is in progress.
    /// </summary>
    internal sealed class OperationAsyncState
    {
        /// <summary>
        /// A unique identifier for the operation.
        /// </summary>
        public int OperationId { get; set; }

        /// <summary>
        /// The <see cref="IConnection"/> object used during the asynchronous operation.
        /// </summary>
        public IConnection Connection { get; set; }

        /// <summary>
        /// A read/write buffer that defaults to 512bytes
        /// </summary>
        public byte[] Buffer = new byte[512];

        /// <summary>
        /// The temporary stream for holding data for the current operation.
        /// </summary>
        public MemoryStream Data = new MemoryStream();

        /// <summary>
        /// The <see cref="OperationHeader"/> of the current operation.
        /// </summary>
        public OperationHeader Header;

        /// <summary>
        /// The <see cref="OperationBody"/> of the current operation.
        /// </summary>
        public OperationBody Body;

        /// <summary>
        /// A current count of the bytes recieved for the current operation.
        /// </summary>
        public int BytesReceived { get; set; }

        public SslStream Stream { get; set; }

        public int Offset { get; set; }

        /// <summary>
        /// Sets all values back to their defaults, so this object can be reused.
        /// </summary>
        public void Reset()
        {
            if(Data != null)
            {
                Data.Dispose();
            }
            Buffer = new byte[512];
            Data = new MemoryStream();
            BytesReceived = 0;
            Header = new OperationHeader();
            Body = new OperationBody();
        }
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

#endregion
