using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Couchbase.Utils
{
    public static class TaskExtensions
    {
        /// <summary>
        /// Makes the <see cref="Task"/> resume without the current context.
        /// </summary>
        /// <param name="task">The current <see cref="Task"/>.</param>
        /// <returns>The <see cref="ConfiguredTaskAwaitable"/> not dependent on the current context.</returns>
        public static ConfiguredTaskAwaitable ContinueOnAnyContext(this Task task)
        {
            return task.ConfigureAwait(false);
        }

        /// <summary>
        /// Makes the <see cref="Task{T}"/> resume without the current context.
        /// </summary>
        /// <param name="task">The current <see cref="Task{T}"/>.</param>
        /// <returns>The <see cref="ConfiguredTaskAwaitable{T}"/> not dependent on the current context.</returns>
        public static ConfiguredTaskAwaitable<T> ContinueOnAnyContext<T>(this Task<T> task)
        {
            return task.ConfigureAwait(false);
        }
    }
}
