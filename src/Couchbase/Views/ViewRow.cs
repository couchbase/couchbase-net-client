using System;
using System.Diagnostics.CodeAnalysis;

#nullable enable

namespace Couchbase.Views
{
    /// <summary>
    /// A row returned by a view query.
    /// </summary>
    /// <typeparam name="TKey">Type of the key for each result row.</typeparam>
    /// <typeparam name="TValue">Type of the value for each result row.</typeparam>
    public interface IViewRow<out TKey, out TValue>
    {
        /// <summary>
        /// The identifier for the row.
        /// </summary>
        string? Id { get; }

        /// <summary>
        /// The key emitted by the View Map function.
        /// </summary>
        [AllowNull]
        TKey Key { get; }

        /// <summary>
        /// The value emitted by the View Map function or if a Reduce view, the value of the Reduce.
        /// </summary>
        [AllowNull]
        TValue Value { get; }
    }

    /// <summary>
    /// A row returned by a view query.
    /// </summary>
    /// <typeparam name="TKey">Type of the key for each result row.</typeparam>
    /// <typeparam name="TValue">Type of the value for each result row.</typeparam>
    internal class ViewRow<TKey, TValue> : IViewRow<TKey, TValue>
    {
        /// <inheritdoc />
        public string? Id { get; set; }

        /// <inheritdoc />
        [AllowNull]
        public TKey Key { get; set; } = default!;

        /// <inheritdoc />
        [AllowNull]
        public TValue Value { get; set; } = default!;
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
