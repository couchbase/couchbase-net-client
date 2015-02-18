using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Couchbase.Utils
{
    public static class TaskExtensions
    {
        /// <summary>
        /// Makes the <see cref="Task"/> resume on the <see cref="Thread"/> that completed the <see cref="Task"/>.
        /// </summary>
        /// <param name="task">The current <see cref="Task"/>.</param>
        /// <returns>The current <see cref="Task"/></returns>
        public static Task ContinueOnAnyContext(this Task task)
        {
            task.ConfigureAwait(false);
            return task;
        }

        /// <summary>
        /// Makes the <see cref="Task{T}"/> resume on the <see cref="Thread"/> that completed the <see cref="Task{T}"/>.
        /// </summary>
        /// <param name="task">The current <see cref="Task{T}"/>.</param>
        /// <returns>The current <see cref="Task{T}"/></returns>
        public static Task<T> ContinueOnAnyContext<T>(this Task<T> task)
        {
            task.ConfigureAwait(false);
            return task;
        }
    }
}
