#nullable enable

namespace System.Threading.Tasks
{
    /// <summary>
    /// Polyfills for Task functionality available in modern .NET but missing in down-level frameworks.
    /// </summary>
    internal static class TaskExtensions
    {
        extension(Task task)
        {
            // These forward to the TimeProvider-based polyfills provided by the Microsoft.Bcl.TimeProvider package.

            /// <summary>Gets a <see cref="Task"/> that will complete when this <see cref="Task"/> completes or when the specified <see cref="CancellationToken"/> has cancellation requested.</summary>
            /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for a cancellation request.</param>
            /// <returns>The <see cref="Task"/> representing the asynchronous wait.  It may or may not be the same instance as the current instance.</returns>
            public Task WaitAsync(CancellationToken cancellationToken) =>
                task.WaitAsync(Timeout.InfiniteTimeSpan, TimeProvider.System, cancellationToken);

            /// <summary>Gets a <see cref="Task"/> that will complete when this <see cref="Task"/> completes, when the specified timeout expires, or when the specified <see cref="CancellationToken"/> has cancellation requested.</summary>
            /// <param name="timeout">The timeout after which the <see cref="Task"/> should be faulted with a <see cref="TimeoutException"/> if it hasn't otherwise completed.</param>
            /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for a cancellation request.</param>
            /// <returns>The <see cref="Task"/> representing the asynchronous wait.  It may or may not be the same instance as the current instance.</returns>
            public Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken = default) =>
                task.WaitAsync(timeout, TimeProvider.System, cancellationToken);
        }

        extension<T>(Task<T> task)
        {
            // These forward to the TimeProvider-based polyfills provided by the Microsoft.Bcl.TimeProvider package.

            /// <summary>Gets a <see cref="Task"/> that will complete when this <see cref="Task"/> completes or when the specified <see cref="CancellationToken"/> has cancellation requested.</summary>
            /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for a cancellation request.</param>
            /// <returns>The <see cref="Task"/> representing the asynchronous wait.  It may or may not be the same instance as the current instance.</returns>
            public Task<T> WaitAsync(CancellationToken cancellationToken) =>
                task.WaitAsync(Timeout.InfiniteTimeSpan, TimeProvider.System, cancellationToken);

            /// <summary>Gets a <see cref="Task"/> that will complete when this <see cref="Task"/> completes, when the specified timeout expires, or when the specified <see cref="CancellationToken"/> has cancellation requested.</summary>
            /// <param name="timeout">The timeout after which the <see cref="Task"/> should be faulted with a <see cref="TimeoutException"/> if it hasn't otherwise completed.</param>
            /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for a cancellation request.</param>
            /// <returns>The <see cref="Task"/> representing the asynchronous wait.  It may or may not be the same instance as the current instance.</returns>
            public Task<T> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken = default) =>
                task.WaitAsync(timeout, TimeProvider.System, cancellationToken);
        }

        extension(ValueTask)
        {
            /// <summary>Creates a <see cref="ValueTask"/> that has completed due to cancellation with the specified cancellation token.</summary>
            /// <param name="cancellationToken">The cancellation token with which to complete the task.</param>
            /// <returns>The canceled task.</returns>
            public static ValueTask FromCanceled(CancellationToken cancellationToken) =>
                new ValueTask(Task.FromCanceled(cancellationToken));

            /// <summary>Creates a <see cref="ValueTask{TResult}"/> that has completed due to cancellation with the specified cancellation token.</summary>
            /// <param name="cancellationToken">The cancellation token with which to complete the task.</param>
            /// <returns>The canceled task.</returns>
            public static ValueTask<T> FromCanceled<T>(CancellationToken cancellationToken) =>
                new ValueTask<T>(Task.FromCanceled<T>(cancellationToken));
        }
    }
}
