using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.IO.Utils;

namespace Couchbase.IO
{
    /// <summary>
    /// Represents a TCP connection to a CouchbaseServer.
    /// </summary>
    internal sealed class Connection : ConnectionBase
    {
        private readonly NetworkStream _stream;

        internal Connection(ConnectionPool<Connection> connectionPool, Socket socket, IByteConverter converter)
            : this(connectionPool, socket, new NetworkStream(socket), converter)
        {
        }

        internal Connection(ConnectionPool<Connection> connectionPool, Socket socket, NetworkStream networkStream,
            IByteConverter converter)
            : base(socket, converter)
        {
            ConnectionPool = connectionPool;
            _stream = networkStream;
            Configuration = ConnectionPool.Configuration;
        }

        public override async Task<uint> SendAsync(byte[] buffer)
        {
            var opaque = Converter.ToUInt32(buffer, HeaderIndexFor.Opaque);
            await _stream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            return opaque;
        }

        public override async Task<byte[]> ReceiveAsync(uint opaque)
        {
            var bytesRead = 0;
            var buffer = new byte[24];
            var bodyLength = 0;

            using (var dataRead = new MemoryStream())
            {
                do
                {
                    bytesRead += await _stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                    if (dataRead.Length == 0)
                    {
                        bodyLength = Converter.ToInt32(buffer, HeaderIndexFor.Body);
                    }
                    dataRead.Write(buffer, 0, buffer.Length);
                    buffer = new byte[bodyLength];
                } while (bytesRead < bodyLength + 24);
                return dataRead.ToArray();
            }
        }

        public override IOperationResult<T> Send<T>(IOperation<T> operation)
        {
            try
            {
                var buffer = operation.Write();
                _stream.BeginWrite(buffer, 0, buffer.Length, SendCallback, operation);
                if (!SendEvent.WaitOne(Configuration.ConnectionTimeout))
                {
                    const string msg =
                        "The connection has timed out while an operation was in flight. The default is 15000ms.";
                    operation.HandleClientError(msg, ResponseStatus.ClientFailure);
                    IsDead = true;
                }
            }
            catch (Exception e)
            {
                HandleException(e, operation);
            }
            return operation.GetResult();
        }

        private void SendCallback(IAsyncResult asyncResult)
        {
            var operation = (IOperation)asyncResult.AsyncState;
            try
            {
                _stream.EndWrite(asyncResult);
                operation.Buffer = BufferManager.TakeBuffer(512);
                _stream.BeginRead(operation.Buffer, 0, operation.Buffer.Length, ReceiveCallback, operation);
            }
            catch (Exception e)
            {
                HandleException(e, operation);
            }
        }

        private void ReceiveCallback(IAsyncResult asyncResult)
        {
            var operation = (IOperation)asyncResult.AsyncState;
            try
            {
                var bytesRead = _stream.EndRead(asyncResult);
                if (bytesRead == 0)
                {
                    SendEvent.Set();
                    return;
                }
                operation.Read(operation.Buffer, 0, bytesRead);
                BufferManager.ReturnBuffer(operation.Buffer);

                if (operation.LengthReceived < operation.TotalLength)
                {
                    operation.Buffer = BufferManager.TakeBuffer(512);
                    _stream.BeginRead(operation.Buffer, 0, operation.Buffer.Length, ReceiveCallback, operation);
                }
                else
                {
                    SendEvent.Set();
                }
            }
            catch (Exception e)
            {
                HandleException(e, operation);
            }
        }

        public override void Dispose()
        {
            Log.Debug(m => m("Disposing connection for {0} - {1}", ConnectionPool.EndPoint, _identity));
            if (!Disposed)
            {
                GC.SuppressFinalize(this);
                if (Socket != null)
                {
                    if (Socket.Connected)
                    {
                        Socket.Shutdown(SocketShutdown.Both);
                        Socket.Close(ConnectionPool.Configuration.ShutdownTimeout);
                    }
                    else
                    {
                        Socket.Close();
                        Socket.Dispose();
                    }
                }
                if (_stream != null)
                {
                    _stream.Dispose();
                }
            }
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
