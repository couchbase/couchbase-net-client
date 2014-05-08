// http://blogs.msdn.com/b/pfxteam/archive/2011/12/15/10248293.aspx
using System;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Couchbase.IO.Strategies.Awaitable
{
    /// <summary>
    /// A class wrapper for <see cref="SocketAsyncEventArgs"/> which supports await and async on <see cref="Socket"/> objects.
    /// </summary>
    public sealed class SocketAwaitable : INotifyCompletion
    {
        private static readonly Action Sentinal = () => { };
        private bool _completed;
        private Action _continuation;
        private readonly SocketAsyncEventArgs _eventArgs;

        /// <summary>
        /// Ctor for <see cref="SocketAwaitable"/>.
        /// </summary>
        /// <param name="eventArgs">The <see cref="SocketAsyncEventArgs"/> object to use for the underlying IO operations.</param>
        /// <exception cref="ArgumentNullException"></exception>
        public SocketAwaitable(SocketAsyncEventArgs eventArgs)
        {
            if (eventArgs == null) throw new ArgumentNullException("eventArgs");

            _eventArgs = eventArgs;
            eventArgs.Completed += delegate
            {
                var previous = _continuation ??
                    Interlocked.
                    CompareExchange(ref _continuation, Sentinal, null);

                if (previous != null)
                {
                    previous();
                }
            };
        }

        /// <summary>
        /// Gets the internal <see cref="SocketAsyncEventArgs"/> object that is wrapped by this instance.
        /// </summary>
        internal SocketAsyncEventArgs EventArgs
        {
            get { return _eventArgs; }
        }

        /// <summary>
        /// Returns true if the operation has comepleted.
        /// </summary>
        public bool IsCompleted
        {
            get { return _completed; }
            set { _completed = value; }
        }

        /// <summary>
        /// Resets the object for reuse.
        /// </summary>
        internal void Reset()
        {
            _completed = false;
            _continuation = null;
        }

        /// <summary>
        /// Gets the object being awaited on.
        /// </summary>
        /// <returns>A <see cref="SocketAwaitable"/> object.</returns>
        public SocketAwaitable GetAwaiter()
        {
            return this;
        }

        /// <summary>
        /// Fired when the asyncrounous operation has completed.
        /// </summary>
        /// <param name="continuation">The <see cref="Action"/> object to run if continuation is required.</param>
        public void OnCompleted(Action continuation)
        {
            if (_continuation == Sentinal ||
                Interlocked.
                CompareExchange(ref _continuation, continuation, null) == Sentinal)
            {
                Task.Run(continuation);
            }
        }

        /// <summary>
        /// Gets the result of the asynchronous <see cref="Socket"/> operation.
        /// </summary>
        /// <remarks>Throws <see cref="SocketException"/> if <see cref="SocketError"/> is not <see cref="SocketError.Success"/>.</remarks>
        /// <exception cref="SocketException"></exception>
        public void GetResult()
        {
            if (_eventArgs.SocketError != SocketError.Success)
            {
                throw new SocketException((int) _eventArgs.SocketError);
            }
        }
    }
}
