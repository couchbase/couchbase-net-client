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

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
