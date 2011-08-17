using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Couchbase
{
	public interface IView : IEnumerable<IViewRow>
	{
		/// <summary>
		/// The view will return only the specified number of items.
		/// </summary>
		/// <param name="value">The number of items to return.</param>
		/// <returns>A new <see cref="T:Couchbase.IView"/> instance that contains the specified number of items from the start of the index in the view.</returns>
		IView Limit(int value);

		/// <summary>
		/// Bypasses the specified number of elements in the view then returns the remaining items.
		/// </summary>
		/// <param name="value">The number of elements to skip before returning the remaining items.</param>
		/// <returns></returns>
		/// <returns>A <see cref="T:Couchbase.IView"/> that contains the items that occur after the specified index in the view.</returns>
		IView Skip(int value);

		/// <summary>
		/// Couchbase will not update the view before returning the data even if it contains stale values. Use this mode if you favor improved query latency over data constistency.
		/// </summary>
		/// <param name="value"></param>
		/// <returns>A new <see cref="T:Couchbase.IView"/> instance that includes the stale items from the view.</returns>
		IView Stale(StaleMode mode);

		/// <summary>
		/// Sort the items of the view in descending order.
		/// </summary>
		/// <returns>A new <see cref="T:Couchbase.IView"/> whose elements are sorted in descending order .</returns>
		IView Descending(bool descending);

		/// <summary>
		/// Start of key range
		/// </summary>
		/// <param name="from"></param>
		/// <returns>TBD</returns>
		IView StartKey(string from);

		/// <summary>
		/// End of key range
		/// </summary>
		/// <param name="to"></param>
		/// <returns>TBD</returns>
		IView EndKey(string to);

		/// <summary>
		/// Start of document id range
		/// </summary>
		/// <param name="from"></param>
		/// <returns>TBD</returns>
		IView StartDocumentId(string from);

		/// <summary>
		/// End of document id range
		/// </summary>
		/// <param name="to"></param>
		/// <returns>TBD</returns>
		IView EndDocumentId(string to);

		/// <summary>
		/// Specifies whether Couchbase must run the view's reduce function.
		/// </summary>
		/// <param name="reduce">A value that specifies whether to run the reduce function.</param>
		/// <returns>A new <see cref="T:Couchbase.IView"/> that can be used to retrieve the items.</returns>
		IView Reduce(bool reduce);

		/// <summary>
		/// Specifies whether the reduce function reduces items to a set of distinct keys or to a single result row.
		/// </summary>
		/// <param name="group">A value that specifies the behavior of the reduce function.</param>
		/// <returns>A new <see cref="T:Couchbase.IView"/> that can be used to retrieve the items.</returns>
		IView Group(bool group);

		/// <summary>
		///	Specifies how many items of the key array are used to group the items by the reduce function.
		/// </summary>
		/// <param name="level">The level of required grouping.</param>
		/// <returns>A new <see cref="T:Couchbase.IView"/> that can be used to retrieve the items.</returns>
		IView GroupAt(int level);

		/// <summary>
		/// Specifies whether the end of the range (document or view key) is included in the result.
		/// </summary>
		/// <param name="inclusive">A value that specifies whether the end of the range is included in the result.</param>
		/// <returns>A new <see cref="T:Couchbase.IView"/> that can be used to retrieve the items.</returns>
		IView WithInclusiveEnd(bool inclusive);

		/// <summary>
		/// Returns a view which allows the user to page retrieve all items of an index in pages.
		/// </summary>
		/// <param name="pageSize"></param>
		/// <returns></returns>
		IPagedView GetPagedView(int pageSize);
	}
}

#region [ License information          ]
/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2011 Couchbase, Inc.
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
