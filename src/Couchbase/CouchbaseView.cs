using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using Enyim.Caching;
using System.Collections;

namespace Couchbase
{
	/// <summary>
	/// Represents the results of a Couchbase index.
	/// </summary>
	internal class CouchbaseView : CouchbaseViewBase<IViewRow>
	{
        internal CouchbaseView(ICouchbaseClient client, IHttpClientLocator clientLocator, string designDocument, string indexName)
            : base(client, clientLocator, designDocument, indexName) { }

        protected CouchbaseView(CouchbaseViewBase<IViewRow> original)
            : base(original) { }

        public override IEnumerator<IViewRow> GetEnumerator() {

            return TransformResults<IViewRow>((jr) => new __Row(this, Json.Parse(jr) as IDictionary<string, object>));

        }        			
		
		#region [ __Row                        ]

		private class __Row : IViewRow
		{
			private readonly CouchbaseView owner;
			private readonly object[] key;
			private readonly string id;
			private readonly IDictionary<string, object> info;

			public __Row(CouchbaseView owner, IDictionary<string, object> row)
			{
				this.owner = owner;

				if (row == null) throw new ArgumentNullException("row", "Missing row info");

				if (!row.TryGetValue("id", out this.id))
					this.id = null; //this is the case when the row is from a reduced view

				object tempKey;

				if (!row.TryGetValue("key", out tempKey))
					throw new InvalidOperationException("The value 'key' was not found in the row definition.");

				this.key = (tempKey as object[]) ?? (new object[] { tempKey });
				this.info = row.AsReadOnly();
			}

			string IViewRow.ItemId
			{
				get { return this.id; }
			}

			object[] IViewRow.ViewKey
			{
				get { return this.key; }
			}

			object IViewRow.GetItem()
			{
				return this.owner.ViewHandler.Client.Get(this.id);
			}

			IDictionary<string, object> IViewRow.Info
			{
				get { return this.info; }
			}
		}

		#endregion
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
