using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Couchbase
{
	public interface ICouchbaseView : IEnumerable<ICouchbaseViewRow>
	{
		/// <summary>
		/// The view will return only the specified number of items.
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		ICouchbaseView Limit(int value);

		/// <summary>
		/// Bypasses the specified number of elements in the view then returns the remaining items.
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		ICouchbaseView Skip(int value);

		/// <summary>
		/// Couchbase will not update the view before erturning the data even if it contains stale values. Use this mode if you favor improved query latency over data constistency.
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		ICouchbaseView Stale();

		/// <summary>
		/// Orders the items of the view in descending order.
		/// </summary>
		/// <returns></returns>
		ICouchbaseView OrderByDescending();

		/// <summary>
		/// Only return items with keys in the specified range.
		/// </summary>
		/// <param name="from"></param>
		/// <param name="to"></param>
		/// <returns></returns>
		ICouchbaseView Range(string from, string to);
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
