using System;
using Couchbase.Core.IO.Serializers;

#nullable enable

namespace Couchbase.Views
{
    public interface IViewRow
    {
        /// <summary>
        /// The identifier for the row
        /// </summary>
        string? Id { get; }

        /// <summary>
        /// The key emitted by the View Map function
        /// </summary>
        TKey Key<TKey>();

        /// <summary>
        /// The value emitted by the View Map function or if a Reduce view, the value of the Reduce
        /// </summary>
        TValue Value<TValue>();
    }

    /// <summary>
    /// Represents a single row returned from a View request
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class ViewRow : IViewRow
    {
        /// <inheritdoc />
        public string? Id { get; set; }

        /// <summary>
        /// Key for the row.
        /// </summary>
        public IJsonToken? KeyToken { get; set; }

        /// <summary>
        /// Value for the row.
        /// </summary>
        public IJsonToken? ValueToken { get; set; }

        /// <summary>
        /// The key emitted by the View Map function
        /// </summary>
        public TKey Key<TKey>()
        {
            return KeyToken != null ? KeyToken.ToObject<TKey>() : default;
        }

        /// <summary>
        /// The value emitted by the View Map function or if a Reduce view, the value of the Reduce
        /// </summary>
        public TValue Value<TValue>()
        {
            return ValueToken != null ? ValueToken.ToObject<TValue>() : default;
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
