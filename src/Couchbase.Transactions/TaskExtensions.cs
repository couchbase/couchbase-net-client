using System.Runtime.CompilerServices;
using System.Threading.Tasks;
#pragma warning disable CS1591

namespace Couchbase.Transactions
{
    /// <summary>
    /// Extension methods to the <see cref="Task"/> and <see cref="ValueTask"/> classes.
    /// </summary>
    public static class TaskExtensions
    {
        public static ConfiguredTaskAwaitable CAF(this Task awaitable) => awaitable.ConfigureAwait(false);
        public static ConfiguredTaskAwaitable<T> CAF<T>(this Task<T> awaitable) => awaitable.ConfigureAwait(false);
        public static ConfiguredValueTaskAwaitable CAF(this ValueTask awaitable) => awaitable.ConfigureAwait(false);
        public static ConfiguredValueTaskAwaitable<T> CAF<T>(this ValueTask<T> awaitable) => awaitable.ConfigureAwait(false);
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
