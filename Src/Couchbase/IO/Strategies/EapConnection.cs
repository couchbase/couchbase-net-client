using System.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using System;
using System.Net.Sockets;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading;

namespace Couchbase.IO.Strategies
{
    internal sealed class EapConnection : ConnectionBase
    {
        private readonly ConnectionPool<EapConnection> _connectionPool;
        private readonly NetworkStream _networkStream;
        private readonly AutoResetEvent _sendEvent = new AutoResetEvent(false);
        private volatile bool _disposed;

        internal EapConnection(ConnectionPool<EapConnection> connectionPool, Socket socket, IByteConverter converter)
            : this(connectionPool, socket, new NetworkStream(socket), converter)
        {
        }

        internal EapConnection(ConnectionPool<EapConnection> connectionPool, Socket socket, NetworkStream networkStream, IByteConverter converter)
            : base(socket, converter)
        {
            _connectionPool = connectionPool;
            _networkStream = networkStream;
        }

        public override IOperationResult<T> Send<T>(IOperation<T> operation)
        {
            try
            {
                operation.Reset();
                var buffer = operation.Write();
                var index = operation.VBucket == null ? 0 : operation.VBucket.Index;
                Log.Info(m=>m("Sending key {0} using {1} on {2}", operation.Key,index, Socket.RemoteEndPoint));
                _networkStream.BeginWrite(buffer, 0, buffer.Length, SendCallback, operation);

                if (!_sendEvent.WaitOne(500))
                {
                    operation.HandleClientError("Timed out.");
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
                _networkStream.EndWrite(asyncResult);
                operation.Buffer = BufferManager.TakeBuffer(512);
                _networkStream.BeginRead(operation.Buffer, 0, operation.Buffer.Length, ReceiveCallback, operation);
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
                var bytesRead = _networkStream.EndRead(asyncResult);
                if (bytesRead == 0)
                {
                    _sendEvent.Set();
                    return;
                }
                operation.Read(operation.Buffer, 0, bytesRead);
                BufferManager.ReturnBuffer(operation.Buffer);

                if (operation.LengthReceived < operation.TotalLength)
                {
                    operation.Buffer = BufferManager.TakeBuffer(512);
                    _networkStream.BeginRead(operation.Buffer, 0, operation.Buffer.Length, ReceiveCallback, operation);
                }
                else
                {
                    _sendEvent.Set();
                }
            }
            catch (Exception e)
            {
                HandleException(e, operation);
            }
        }

        private void HandleException(Exception e, IOperation operation)
        {
            try
            {
                var message = string.Format("Opcode={0} | Key={1} | Host={2}",
                    operation.OperationCode,
                    operation.Key,
                    _connectionPool.EndPoint);

                Log.Warn(message, e);
                WriteError("Failed. Check Exception property.", operation, 0);
                operation.Exception = e;
            }
            finally
            {
                _sendEvent.Set();
            }
        }

        private static void WriteError(string errorMsg, IOperation operation, int offset)
        {
            var bytes = Encoding.UTF8.GetBytes(errorMsg);
            operation.Read(bytes, offset, errorMsg.Length);
        }

        /// <summary>
        /// Shuts down, closes and disposes of the internal <see cref="Socket"/> instance.
        /// </summary>
        public override void Dispose()
        {
            Log.Debug(m => m("Disposing connection for {0} - {1}", _connectionPool.EndPoint, _identity));
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!_disposed)
                {
                    if (Socket != null)
                    {
                        if (Socket.Connected)
                        {
                            Socket.Shutdown(SocketShutdown.Both);
                            Socket.Close(_connectionPool.Configuration.ShutdownTimeout);
                        }
                        else
                        {
                            Socket.Close();
                            Socket.Dispose();
                        }
                    }
                    if (_networkStream != null)
                    {
                        _networkStream.Dispose();
                    }
                }
            }
            else
            {
                if (!_disposed)
                {
                    if (Socket != null)
                    {
                        Socket.Close();
                        Socket.Dispose();
                    }
                    if (_networkStream != null)
                    {
                        _networkStream.Dispose();
                    }
                }
            }
            _disposed = true;
        }

        ~EapConnection()
        {
            Log.Debug(m=>m("Finalizing connection for {0}", _connectionPool.EndPoint));
            Dispose(false);
        }
    }
}