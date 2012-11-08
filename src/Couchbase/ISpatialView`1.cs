using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Couchbase
{
	public interface ISpatialView<T> : IEnumerable<T>
	{
		/// <summary>
		/// The view will return only the specified number of items.
		/// </summary>
		/// <param name="value">The number of items to return.</param>
		/// <returns>A new <see cref="T:Couchbase.ISpatialView<T>"/> instance that contains the specified number of items from the start of the index in the view.</returns>
		ISpatialView<T> Limit(int value);

		/// <summary>
		/// Bypasses the specified number of elements in the view then returns the remaining items.
		/// </summary>
		/// <param name="value">The number of elements to skip before returning the remaining items.</param>
		/// <returns></returns>
		/// <returns>A <see cref="T:Couchbase.ISpatialView"/> that contains the items that occur after the specified index in the view.</returns>
		ISpatialView<T> Skip(int value);

		/// <summary>
		/// Couchbase will not update the view before returning the data even if it contains stale values. Use this mode if you favor improved query latency over data constistency.
		/// </summary>
		/// <param name="value"></param>
		/// <returns>A new <see cref="T:Couchbase.ISpatialView<T>"/> instance that includes the stale items from the view.</returns>
		ISpatialView<T> Stale(StaleMode mode);

		/// <summary>
		/// Bounding box for all rows to be returned by spatial view query
		/// </summary>
		/// <param name="value"></param>
		/// <returns>A new <see cref="T:Couchbase.ISpatialView<T>"/> instance that includes all records within the bounding box.</returns>
		ISpatialView<T> BoundingBox(float lowerLeftLong, float lowerLeftLat, float upperRightLong, float upperRightLat);

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