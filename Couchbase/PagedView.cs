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

namespace Couchbase
{
	internal class PagedView : IPagedView
	{
		private IView currentView;

		private int pageIndex;
		private int pageSize;
		private List<IViewRow> items;

		private string nextId;
		private int state;

		public PagedView(IView view, int pageSize)
		{
			this.items = new List<IViewRow>();
			this.pageSize = pageSize;
			this.currentView = view.EndDocumentId(null);

			this.state = -1;
		}

		bool IPagedView.MoveNext()
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

		int IPagedView.PageSize
		{
			get { return this.pageSize; }
		}

		int IPagedView.PageIndex
		{
			get { return this.pageIndex; }
		}

		private string LoadData(IView view)
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
				else
					lastId = row.ItemId;

				count--;
			}

			return lastId;
		}

		IEnumerator<IViewRow> IEnumerable<IViewRow>.GetEnumerator()
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
