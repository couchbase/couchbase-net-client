using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using Hammock;
using Hammock.Serialization;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using Enyim.Caching;
using System.Reflection;

namespace Couchbase
{
	internal class PagedView<T> : IPagedView<T>
	{
		private static Dictionary<Type, Tuple<PropertyInfo,PropertyInfo>> propertyCache = new Dictionary<Type, Tuple<PropertyInfo, PropertyInfo>>();

		private IView<T> currentView;

		private int pageIndex;
		private int pageSize;
		private List<T> items;

		private string nextId;
		private object nextKey;
		private int state;
		private int totalRows;

		private readonly string _pagedViewIdProperty;
		private readonly string _pagedViewKeyProperty;

		public PagedView(IView<T> view, int pageSize, string pagedViewIdProperty = null, string pagedViewKeyProperty = null)
		{
			this.items = new List<T>();
			this.pageSize = pageSize;
			this.currentView = view.EndDocumentId(null);

			this.state = -1;

			_pagedViewIdProperty = pagedViewIdProperty;
			_pagedViewKeyProperty = pagedViewKeyProperty;
		}

		public bool MoveNext()
		{
			Tuple<string, object> lastIdAndKey;

			if (this.state == -1)
			{
				lastIdAndKey = this.LoadData(this.currentView);
				this.nextId = lastIdAndKey.Item1;
				this.nextKey = lastIdAndKey.Item2;
				this.state = 1;

				return this.nextId != null;
			}

			// can't go further
			if (this.nextId == null || this.items.Count == 0)
				return false;

			// get a reference to the current page
			this.currentView.StartDocumentId(this.nextId);
			this.currentView.StartKey(this.nextKey);

			lastIdAndKey = this.LoadData(this.currentView);
			this.nextId = lastIdAndKey.Item1;
			this.nextKey = lastIdAndKey.Item2;
			this.pageIndex++;

			// did not load anything, we're at the end
			return this.items.Count != 0;
		}

		public int PageSize
		{
			get { return this.pageSize; }
		}

		public int PageIndex
		{
			get { return this.pageIndex; }
		}

		public int TotalRows
		{
			get { return this.totalRows; }
		}

		private Tuple<string, object> LoadData(IView<T> view)
		{
			this.items.Clear();

			var count = this.pageSize;
			var lastIdAndKey = Tuple.Create<string, object>(null, null);

			foreach (var row in view.Limit(this.pageSize + 1))
			{
				this.totalRows = view.TotalRows;

				// only store pageSize count of items
				// the last one will be only stored as a reference to the next page
				if (count > 0)
					this.items.Add(row);
				else
				{
					//HACK: This needs to be a transform function or something
					if (row is IViewRow)
					{
						var viewRow = (IViewRow)row;
						return Tuple.Create(viewRow.ItemId, viewRow.Info["key"]);
					}
					else
					{
						var genericView = view as CouchbaseView<T>;

						if (string.IsNullOrEmpty(_pagedViewIdProperty) ||
							string.IsNullOrEmpty(_pagedViewKeyProperty))
							throw new InvalidOperationException("Generic view paging requires setting the view id and key properties when calling GetView<T>");

						var typeOfRow = row.GetType();
						if (!propertyCache.ContainsKey(typeOfRow))
						{
							var idProperty = typeOfRow.GetProperties().FirstOrDefault(p => p.Name == _pagedViewIdProperty);
							var keyProperty = typeOfRow.GetProperties().FirstOrDefault(p => p.Name == _pagedViewKeyProperty);
							propertyCache[typeOfRow] = Tuple.Create(idProperty, keyProperty);
						}

						var lastId = propertyCache[typeOfRow].Item1.GetValue(row, null) as string;
						var lastKey = propertyCache[typeOfRow].Item2.GetValue(row, null);
						return Tuple.Create(lastId, lastKey);
					}
				}

				count--;
			}

			return lastIdAndKey;
		}

		IEnumerator<T> IEnumerable<T>.GetEnumerator()
		{
			return this.items.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.items.GetEnumerator();
		}
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
