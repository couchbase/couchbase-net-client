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
	/// <summary>
	/// Represents the results of a Couchbase index.
	/// </summary>
	public class CouchbaseView : ICouchbaseView
	{
		private static readonly Enyim.Caching.ILog log = Enyim.Caching.LogManager.GetLogger(typeof(CouchbaseView));

		private readonly IMemcachedClient client;
		private readonly IHttpClientLocator clientLocator;
		private readonly string designDocument;
		private readonly string indexName;

		private string endKey;
		private string startKey;
		private string endId;
		private string startId;
		private bool? descending;
		private int? skip;
		private int? limit;
		private bool? reduce;
		private bool stale;

		internal CouchbaseView(IMemcachedClient client, IHttpClientLocator clientLocator, string designDocument, string indexName)
		{
			this.client = client;
			this.clientLocator = clientLocator;
			this.designDocument = designDocument;
			this.indexName = indexName;
		}

		protected CouchbaseView(CouchbaseView original)
		{
			this.clientLocator = original.clientLocator;
			this.designDocument = original.designDocument;
			this.indexName = original.indexName;

			this.startKey = original.startKey;
			this.endKey = original.endKey;

			this.startId = original.startId;
			this.endId = original.endId;

			this.descending = original.descending;
			this.reduce = original.reduce;

			this.skip = original.skip;
			this.limit = original.limit;
			this.stale = original.stale;
		}

		/// <summary>
		/// The view will return only the specified number of items.
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		ICouchbaseView ICouchbaseView.Limit(int value)
		{
			return new CouchbaseView(this) { limit = value };
		}

		/// <summary>
		/// Bypasses the specified number of elements in the view then returns the remaining items.
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		ICouchbaseView ICouchbaseView.Skip(int value)
		{
			return new CouchbaseView(this) { skip = value };
		}

		/// <summary>
		/// Couchbase will not update the view before erturning the data even if it contains stale values. Use this mode if you favor improved query latency over data constistency.
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		ICouchbaseView ICouchbaseView.Stale()
		{
			return new CouchbaseView(this) { stale = true };
		}

		/// <summary>
		/// Orders the items of the view in descending order.
		/// </summary>
		/// <returns></returns>
		ICouchbaseView ICouchbaseView.OrderByDescending()
		{
			return new CouchbaseView(this) { descending = true };
		}

		/// <summary>
		/// Only return items with keys in the specified range.
		/// </summary>
		/// <param name="from"></param>
		/// <param name="to"></param>
		/// <returns>A new <see cref="T:ICouchbaseView"/> instance, which when enumerated will return items from the specified range.</returns>
		ICouchbaseView ICouchbaseView.KeyRange(string from, string to)
		{
			return new CouchbaseView(this)
			{
				startKey = from,
				endKey = to
			};
		}

		/// <summary>
		/// Only return items with document ids in the specified range.
		/// </summary>
		/// <param name="from"></param>
		/// <param name="to"></param>
		/// <returns>A new <see cref="T:ICouchbaseView"/> instance, which when enumerated will return the map-reduced items.</returns>
		/// <returns>A new <see cref="T:ICouchbaseView"/> instance, which when enumerated will return items from the specified range.</returns>
		ICouchbaseView ICouchbaseView.IdRange(string from, string to)
		{
			return new CouchbaseView(this)
			{
				startId = from,
				endId = to
			};
		}

		/// <summary>
		/// Run the reduce function on the items.
		/// </summary>
		/// <returns>A new <see cref="T:ICouchbaseView"/> instance, which when enumerated will return the map-reduced items.</returns>
		ICouchbaseView ICouchbaseView.Reduce(bool reduce)
		{
			return new CouchbaseView(this) { reduce = reduce };
		}

		/// <summary>
		/// Builds the request uri based on the parameters set by the user
		/// </summary>
		/// <returns></returns>
		private IHttpRequest CreateRequest(IHttpClient client)
		{
			var retval = client.CreateRequest(this.designDocument + "/_view/" + this.indexName);

			if (this.startKey != null) retval.AddParameter("startKey", this.startKey);
			if (this.endKey != null) retval.AddParameter("endKey", this.endKey);

			if (this.startId != null) retval.AddParameter("startKey_docid", this.startId);
			if (this.endId != null) retval.AddParameter("endKey_docid", this.endId);

			if (this.descending != null) retval.AddParameter("descending", this.descending.Value ? "true" : "false");
			if (this.reduce != null) retval.AddParameter("reduce", this.reduce.Value ? "true" : "false");

			if (this.skip != null) retval.AddParameter("skip", this.skip.ToString());
			if (this.limit != null) retval.AddParameter("limit", this.limit.ToString());
			if (this.stale) retval.AddParameter("stale", "ok");

			return retval;
		}

		public override string ToString()
		{
			return this.designDocument + "/" + this.indexName;
		}

		private IHttpResponse GetResponse()
		{
			Debug.Assert(this.clientLocator != null);

			var client = this.clientLocator.Locate(this.designDocument);
			if (client == null)
			{
				if (log.IsErrorEnabled)
					log.WarnFormat("View {0} was mapped to a dead node, failing.", this);

				throw new InvalidOperationException();
			}

			var request = CreateRequest(client);

			return request.GetResponse();
		}

		IEnumerator<ICouchbaseViewRow> IEnumerable<ICouchbaseViewRow>.GetEnumerator()
		{
			var response = GetResponse();
			Debug.Assert(response != null);

			using (var sr = new StreamReader(response.GetResponseStream(), Encoding.UTF8, true))
			using (var jsonReader = new JsonTextReader(sr))
			{
				// position the reader on the first "rows" field which contains the actual resultset
				// this way we do not have to deserialize the whole response twice
				bool shouldDeserialize = false;

				while (jsonReader.Read())
				{
					if (jsonReader.TokenType == Newtonsoft.Json.JsonToken.PropertyName
						&& jsonReader.Depth == 1
						&& ((string)jsonReader.Value) == "rows")
					{
						if (!jsonReader.Read()
							|| (jsonReader.TokenType != Newtonsoft.Json.JsonToken.StartArray
								&& jsonReader.TokenType != Newtonsoft.Json.JsonToken.Null))
							throw new InvalidOperationException("Expecting array at 'rows'");

						// we skip the deserialization if the array is null
						shouldDeserialize = jsonReader.TokenType == Newtonsoft.Json.JsonToken.StartArray;
						break;
					}
				}

				if (!shouldDeserialize) yield break;

				// read until the end of the rows array
				while (jsonReader.Read() && jsonReader.TokenType != Newtonsoft.Json.JsonToken.EndArray)
				{
					var row = new __Row(this, Json.Parse(jsonReader) as Dictionary<string, object>);

					yield return row;
				}
			}
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<ICouchbaseViewRow>)this).GetEnumerator();
		}

		#region [ __Row                        ]

		private class __Row : ICouchbaseViewRow
		{
			private readonly CouchbaseView owner;
			private readonly string key;
			private readonly string id;
			private readonly Dictionary<string, object> info;

			public __Row(CouchbaseView owner, Dictionary<string, object> row)
			{
				this.owner = owner;

				if (row == null) throw new ArgumentNullException("row", "Missing row info");

				if (!row.TryGetValue("key", out this.key))
					throw new InvalidOperationException("The value 'key' was not found in the row definition.");
				if (!row.TryGetValue("id", out this.id))
					throw new InvalidOperationException("The value 'id' was not found in the row definition.");

				row.TryGetValue("value", out this.info);
			}

			string ICouchbaseViewRow.ItemId
			{
				get { return this.id; }
			}

			string ICouchbaseViewRow.ViewKey
			{
				get { return this.key; }
			}

			object ICouchbaseViewRow.GetItem()
			{
				return this.owner.client.Get(this.id);
			}

			Dictionary<string, object> ICouchbaseViewRow.Info
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
