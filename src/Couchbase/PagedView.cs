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
        private static Dictionary<Type, PropertyInfo> propertyCache = new Dictionary<Type, PropertyInfo>();

		private IView<T> currentView;

		private int pageIndex;
		private int pageSize;
		private List<T> items;

		private string nextId;
		private int state;

        public PagedView(IView<T> view, int pageSize)
        {
            this.items = new List<T>();
            this.pageSize = pageSize;
            this.currentView = view.EndDocumentId(null);

            this.state = -1;
        }

		public bool MoveNext()
		{
			if (this.state == -1)
			{
				this.nextId = this.LoadData(this.currentView);
				this.state = 1;

				return this.nextId != null;
			}

			// can't go further
			if (this.nextId == null || this.items.Count == 0)
				return false;

			// get a reference to the current page
			var page = this.currentView.StartDocumentId(this.nextId);

			this.nextId = this.LoadData(page);
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

		private string LoadData(IView<T> view)
		{
			this.items.Clear();

			var count = this.pageSize;
			string lastId = null;

			foreach (var row in view.Limit(this.pageSize + 1))
			{
				// only store pageSize count of items
				// the last one will be only stored as a reference to the next page
                if (count > 0)
                    this.items.Add(row);
                else {
                    //HACK: This needs to be a transform function or something
                    if (row is IViewRow)
                    {
                        lastId = ((IViewRow)row).ItemId;
                    }
                    else
                    {
                        var typeOfRow = row.GetType();

                        if (! propertyCache.ContainsKey(typeOfRow))
                        {
                            //for strongly typed views suppor possible variations of Id property naming
                            var docIdPropNames = new string[] { "Id", "_Id" };
                            Func<PropertyInfo, bool> idPredicate =
                                p => docIdPropNames.Contains(p.Name, StringComparer.CurrentCultureIgnoreCase);
                            var property = typeOfRow.GetProperties().FirstOrDefault(p => idPredicate(p));
                            propertyCache[typeOfRow] = property;
                        }

                        if (propertyCache[typeOfRow] != null)
                        {
                            lastId = propertyCache[typeOfRow].GetValue(row, null) as string;
                        }
                    }
                }

				count--;
			}

			return lastId;
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
