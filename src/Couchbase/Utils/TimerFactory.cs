using System;
using System.Threading;

namespace Couchbase.Utils
{
    internal static class TimerFactory
    {
        /// <summary>
        /// Creates a <see cref="Timer"/> where the <see cref="ExecutionContext"/> does not flow to the callbacks.
        /// </summary>
        /// <param name="callback">A delegate representing a method to be executed.</param>
        /// <param name="state">An object containing information to be used by the callback method, or <c>null</c>.</param>
        /// <param name="dueTime">The amount of time to delay before the <paramref name="callback"/> is invoked. Specify <see cref="Timeout.InfiniteTimeSpan"/> to prevent the timer from starting. Specify <see cref="TimeSpan.Zero"/> to start the timer immediately.</param>
        /// <param name="period">The time interval between invocations of <paramref name="callback"/>. Specify <see cref="Timeout.InfiniteTimeSpan"/> to disable periodic signaling.</param>
        /// <returns>The new <see cref="Timer"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// The number of milliseconds in the value of <paramref name="dueTime"/> or <paramref name="period"/> is negative and not equal to
        /// <see cref="Timeout.InfiniteTimeSpan"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="callback"/> is <c>null</c>.
        /// </exception>
        /// <remarks>
        /// This is important for long-running timers (i.e. which repeat based on <paramref name="period"/>) to avoid the appearance of memory leaks.
        /// Much of the Couchbase SDK is lazy initialized, meaning it doesn't start up until the first time it is used in a request.
        /// This means that <see cref="AsyncLocal{T}"/> values which are part of the ExecutionContext at bootstrap may continue
        /// to live indefinitely. This may include activity tracing, the first HttpContext, etc, and may cause memory leaks or other
        /// undesired behaviors.
        /// </remarks>
        public static Timer CreateWithFlowSuppressed(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period)
        {
            bool restoreFlow = false;
            try
            {
                if (!ExecutionContext.IsFlowSuppressed())
                {
                    ExecutionContext.SuppressFlow();
                    restoreFlow = true;
                }

                return new Timer(callback, state, dueTime, period);
            }
            finally
            {
                if (restoreFlow)
                {
                    ExecutionContext.RestoreFlow();
                }
            }
        }
    }
}
