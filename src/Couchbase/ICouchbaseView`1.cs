using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Couchbase
{
	public interface IView<T> : IEnumerable<T>
	{
		/// <summary>
		/// TotalRows that would be returned by view, regardless of filters
		/// </summary>
		int TotalRows { get; }

		/// <summary>
		/// Debug info when Debug param is true
		/// </summary>
		IDictionary<string, object> DebugInfo { get; }

		/// <summary>
		/// The view will return only the specified number of items.
		/// </summary>
		/// <param name="value">The number of items to return.</param>
		/// <returns>A new <see cref="T:Couchbase.IView"/> instance that contains the specified number of items from the start of the index in the view.</returns>
		IView<T>Limit(int value);

		/// <summary>
		/// Bypasses the specified number of elements in the view then returns the remaining items.
		/// </summary>
		/// <param name="value">The number of elements to skip before returning the remaining items.</param>
		/// <returns></returns>
		/// <returns>A <see cref="T:Couchbase.IView"/> that contains the items that occur after the specified index in the view.</returns>
		IView<T>Skip(int value);

		/// <summary>
		/// Couchbase will not update the view before returning the data even if it contains stale values. Use this mode if you favor improved query latency over data constistency.
		/// </summary>
		/// <param name="value"></param>
		/// <returns>A new <see cref="T:Couchbase.IView"/> instance that includes the stale items from the view.</returns>
		IView<T>Stale(StaleMode mode);

        /// <summary>
        /// Control the behavior of view engine when returning a request in the event of an error.
        /// </summary>
        /// <param name="mode"></param>
        /// <returns>A new <see cref="T:Couchbase.IView"/> instance potentially abbreviated by error conditions.</returns>
        IView<T> OnError(OnErrorMode mode);

		/// <summary>
		/// Sort the items of the view in descending order.
		/// </summary>
		/// <returns>A new <see cref="T:Couchbase.IView"/> whose elements are sorted in descending order .</returns>
		IView<T>Descending(bool descending);

        /// <summary>
		/// Start of key range
		/// </summary>
		/// <param name="from"></param>
        /// <returns>A new <see cref="T:Couchbase.IView"/> with the row for the given key.</returns>
		IView<T>StartKey<KeyType>(KeyType from);

		/// <summary>
		/// Key of document
		/// </summary>
		/// <param name="key"></param>
        /// <returns>A new <see cref="T:Couchbase.IView"/> for the row of the given key.</returns>
		IView<T> Key<KeyType>(KeyType key);

        /// <summary>
        /// Keys of document set
        /// </summary>
        /// <param name="keys"></param>
        /// <returns>A new <see cref="T:Couchbase.IView"/> for the rows for the given keys.</returns>
        IView<T> Keys<KeyType>(KeyType keys);

		/// <summary>
		/// End of key range
		/// </summary>
		/// <param name="to"></param>
		/// <returns>TBD</returns>
		IView<T>EndKey<KeyType>(KeyType to);

		/// <summary>
		/// Start of document id range
		/// </summary>
		/// <param name="from"></param>
		/// <returns>TBD</returns>
		IView<T>StartDocumentId(string from);

		/// <summary>
		/// End of document id range
		/// </summary>
		/// <param name="to"></param>
		/// <returns>TBD</returns>
		IView<T>EndDocumentId(string to);

		/// <summary>
		/// Specifies whether Couchbase must run the view's reduce function.
		/// </summary>
		/// <param name="reduce">A value that specifies whether to run the reduce function.</param>
		/// <returns>A new <see cref="T:Couchbase.IView"/> that can be used to retrieve the items.</returns>
		IView<T>Reduce(bool reduce);

		/// <summary>
		/// Specifies whether the reduce function reduces items to a set of distinct keys or to a single result row.
		/// </summary>
		/// <param name="group">A value that specifies the behavior of the reduce function.</param>
		/// <returns>A new <see cref="T:Couchbase.IView"/> that can be used to retrieve the items.</returns>
		IView<T>Group(bool group);

		/// <summary>
		///	Specifies how many items of the key array are used to group the items by the reduce function.
		/// </summary>
		/// <param name="level">The level of required grouping.</param>
		/// <returns>A new <see cref="T:Couchbase.IView"/> that can be used to retrieve the items.</returns>
		IView<T>GroupAt(int level);

		/// <summary>
		/// Specifies whether the end of the range (document or view key) is included in the result.
		/// </summary>
		/// <param name="inclusive">A value that specifies whether the end of the range is included in the result.</param>
		/// <returns>A new <see cref="T:Couchbase.IView"/> that can be used to retrieve the items.</returns>
		IView<T>WithInclusiveEnd(bool inclusive);

		/// <summary>
		/// Returns a view which allows the user to page retrieve all items of an index in pages.
		/// </summary>
		/// <param name="pageSize"></param>
		/// <param name="pagedViewIdProperty">When paging over a generic view, this is the property to which the row's ID is mapped</param>
		/// <param name="pagedViewKeyProperty">When paging over a generic view, this is the property to which the row's key is mapped</param>
		/// <returns></returns>
		IPagedView<T> GetPagedView(int pageSize, string pagedViewIdProperty = null, string pagedViewKeyProperty = null);

		/// <summary>
		/// Specifies whether Couchbase should return debug info
		/// </summary>
		/// <param name="debug">A value that specifies whether to include debug info.</param>
		/// <returns>A new <see cref="T:Couchbase.IView"/> that can be used to retrieve the items, including debug info.</returns>
		IView<T> Debug(bool debug);
	}
}

#region [ License information          ]
/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2012 Couchbase, Inc.
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
