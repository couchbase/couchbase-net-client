using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Couchbase.Extensions.DependencyInjection.Internal
{
    /// <summary>
    /// Provides support for asynchronous lazy initialization.
    /// </summary>
    /// <typeparam name="T">The type of object that is being lazily initialized.</typeparam>
    [DebuggerDisplay("IsValueCreated = {" + nameof(IsValueCreated) + "}")]
    [DebuggerTypeProxy(typeof(AsyncLazy<>.DebugView))]
    internal class AsyncLazy<T> : Lazy<Task<T>>
    {
        /// <summary>
        /// Initializes a new instance of the AsyncLazy&gt;T&lt; class.
        /// </summary>
        /// <param name="factoryFunc">Function which is invoked to produce the lazily initialized value when it is needed.</param>
        public AsyncLazy(Func<Task<T>> factoryFunc)
            : base(WrapFactoryFunc(factoryFunc))
        {
        }

        /// <summary>
        /// Initializes a new instance of the AsyncLazy&gt;T&lt; class.
        /// </summary>
        /// <param name="factoryFunc">Function which is invoked to produce the lazily initialized value when it is needed.</param>
        public AsyncLazy(Func<ValueTask<T>> factoryFunc)
            : base(WrapFactoryFunc(factoryFunc))
        {
        }

        private static Func<Task<T>> WrapFactoryFunc(Func<Task<T>> factoryFunc) =>
            () => Task.Run(factoryFunc);

        private static Func<Task<T>> WrapFactoryFunc(Func<ValueTask<T>> factoryFunc) =>
            () => Task.Run(() => factoryFunc.Invoke().AsTask());

        /// <inheritdoc cref="Task{T}.GetAwaiter" />
        public TaskAwaiter<T> GetAwaiter() => Value.GetAwaiter();

        /// <inheritdoc cref="Task{T}.ConfigureAwait" />
        public ConfiguredTaskAwaitable<T> ConfigureAwait(bool continueOnCapturedContext) =>
            Value.ConfigureAwait(continueOnCapturedContext);

        [DebuggerNonUserCode]
        internal sealed class DebugView
        {
            private readonly AsyncLazy<T> _instance;

            public DebugView(AsyncLazy<T> instance)
            {
                _instance = instance ?? throw new ArgumentNullException(nameof(instance));
            }

            public T Value => _instance.IsValueCreated && _instance.Value.IsCompleted
                ? _instance.Value.Result
                : throw new InvalidOperationException("Not created.");
        }
    }
}
