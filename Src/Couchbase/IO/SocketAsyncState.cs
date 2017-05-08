using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Couchbase.IO
{
    public sealed class SocketAsyncState : IDisposable
    {
        public IPEndPoint EndPoint { get; set; }

        public int SendOffset { get; set; }

        public int BytesSent { get; set; }

        public MemoryStream Data { get; set; }

        public int BytesReceived { get; set; }

        public int BodyLength { get; set; }

        public uint Opaque { get; set; }

        private byte[] _buffer;
        public byte[] Buffer
        {
            get { return _buffer; }
            set
            {
                _buffer = value;

                // For compatibility with direct assignments for classes not using IOBuffer
                BufferOffset = 0;
                BufferLength = value != null ? value.Length : 0;
            }
        }

        public int BufferOffset { get; private set; }

        public int BufferLength { get; private set; }

        public Exception Exception { get; set; }

        public Func<SocketAsyncState, Task> Completed { get; set; }

        /// <summary>
        /// Represents a response status that has originated in within the client.
        /// The purpose is to handle client side errors
        /// </summary>
        public ResponseStatus Status { get; set; }

        // ReSharper disable once InconsistentNaming
        internal IOBuffer IOBuffer { get; private set; }

        // ReSharper disable once InconsistentNaming
        internal void SetIOBuffer(IOBuffer ioBuffer)
        {
            if (ioBuffer != null)
            {
                IOBuffer = ioBuffer;

                // Buffer must be assigned first, as it will reset BufferOffset and BufferLength
                Buffer = ioBuffer.Buffer;

                BufferOffset = ioBuffer.Offset;
                BufferLength = ioBuffer.Length;
            }
            else
            {
                IOBuffer = null;
                Buffer = null;
            }
        }

        public void Dispose()
        {
            if (Data != null) Data.Dispose();
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
}
