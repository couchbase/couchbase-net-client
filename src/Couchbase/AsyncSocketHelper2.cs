using System;
using System.Net.Sockets;
using System.Threading;
using Enyim.Caching.Memcached;

namespace Couchbase
{
    /// <summary>
    /// Supports exactly one reader and writer, but they can do IO concurrently
    /// </summary>
    /// <note>This class was a private class in the Enyim.Caching.PooledSocket class. I pulled it into
    /// this assembly because any IPooledSocket is dependent upon it ATM. It will be rewritten or removed.
    /// Since the implmentation hasn't really changed, I just renamed it with a "2" postfix to eliminate any
    /// naming collisions with the 'real' AsyncSocketHelper class</note>
    [Obsolete("Will be removed in later versions")]
    internal class AsyncSocketHelper2
    {
        private const int ChunkSize = 65536;

        private CouchbasePooledSocket socket;
        private SlidingBuffer asyncBuffer;

        private SocketAsyncEventArgs readEvent;
#if DEBUG_IO
			private int doingIO;
#endif
        private int remainingRead;
        private int expectedToRead;
        private AsyncIOArgs pendingArgs;

        private int isAborted;
        private ManualResetEvent readInProgressEvent;

        public AsyncSocketHelper2(CouchbasePooledSocket socket)
        {
            this.socket = socket;
            this.asyncBuffer = new SlidingBuffer(ChunkSize);

            this.readEvent = new SocketAsyncEventArgs();
            this.readEvent.Completed += new EventHandler<SocketAsyncEventArgs>(AsyncReadCompleted);
            this.readEvent.SetBuffer(new byte[ChunkSize], 0, ChunkSize);

            this.readInProgressEvent = new ManualResetEvent(false);
        }

        /// <summary>
        /// returns true if io is pending
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public bool Read(AsyncIOArgs p)
        {
            var count = p.Count;
            if (count < 1) throw new ArgumentOutOfRangeException("count", "count must be > 0");
#if DEBUG_IO
				if (Interlocked.CompareExchange(ref this.doingIO, 1, 0) != 0)
					throw new InvalidOperationException("Receive is already in progress");
#endif
            this.expectedToRead = p.Count;
            this.pendingArgs = p;

            p.Fail = false;
            p.Result = null;

            if (this.asyncBuffer.Available >= count)
            {
                PublishResult(false);

                return false;
            }
            else
            {
                this.remainingRead = count - this.asyncBuffer.Available;
                this.isAborted = 0;

                this.BeginReceive();

                return true;
            }
        }

        public void DiscardBuffer()
        {
            this.asyncBuffer.UnsafeClear();
        }

        private void BeginReceive()
        {
            while (this.remainingRead > 0)
            {
                this.readInProgressEvent.Reset();

                if (this.socket.Socket.ReceiveAsync(this.readEvent))
                {
                    // wait until the timeout elapses, then abort this reading process
                    // EndREceive will be triggered sooner or later but its timeout
                    // may be higher than our read timeout, so it's not reliable
                    if (!readInProgressEvent.WaitOne(this.socket.Socket.ReceiveTimeout))
                        this.AbortReadAndTryPublishError(false);

                    return;
                }

                this.EndReceive();
            }
        }

        void AsyncReadCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (this.EndReceive())
                this.BeginReceive();
        }

        private void AbortReadAndTryPublishError(bool markAsDead)
        {
            if (markAsDead)
                this.socket.Close();

            // we've been already aborted, so quit
            // both the EndReceive and the wait on the event can abort the read
            // but only one should of them should continue the async call chain
            if (Interlocked.CompareExchange(ref this.isAborted, 1, 0) != 0)
                return;

            this.remainingRead = 0;
            var p = this.pendingArgs;
#if DEBUG_IO
				Thread.MemoryBarrier();

				this.doingIO = 0;
#endif

            p.Fail = true;
            p.Result = null;

            this.pendingArgs.Next(p);
        }

        /// <summary>
        /// returns true when io is pending
        /// </summary>
        /// <returns></returns>
        private bool EndReceive()
        {
            this.readInProgressEvent.Set();

            var read = this.readEvent.BytesTransferred;
            if (this.readEvent.SocketError != SocketError.Success
                || read == 0)
            {
                this.AbortReadAndTryPublishError(true);//new IOException("Remote end has been closed"));

                return false;
            }

            this.remainingRead -= read;
            this.asyncBuffer.Append(this.readEvent.Buffer, 0, read);

            if (this.remainingRead <= 0)
            {
                this.PublishResult(true);

                return false;
            }

            return true;
        }

        private void PublishResult(bool isAsync)
        {
            var retval = this.pendingArgs;

            var data = new byte[this.expectedToRead];
            this.asyncBuffer.Read(data, 0, retval.Count);
            pendingArgs.Result = data;
#if DEBUG_IO
				Thread.MemoryBarrier();
				this.doingIO = 0;
#endif

            if (isAsync)
                pendingArgs.Next(pendingArgs);
        }
    }
}
