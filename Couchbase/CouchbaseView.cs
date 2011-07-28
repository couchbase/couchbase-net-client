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
	internal class CouchbaseView : IView
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

		private StaleMode? stale;
		private bool? descending;
		private bool? inclusive;

		private int? skip;
		private int? limit;

		private bool? reduce;
		private bool? group;
		private int? groupAt;

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

			this.stale = original.stale;
			this.descending = original.descending;
			this.inclusive = original.inclusive;

			this.skip = original.skip;
			this.limit = original.limit;

			this.reduce = original.reduce;
			this.groupAt = original.groupAt;
		}

		private static bool MoveToArray(JsonReader reader, int depth, string name)
		{
			while (reader.Read())
			{
				if (reader.TokenType == Newtonsoft.Json.JsonToken.PropertyName
					&& reader.Depth == 1
					&& ((string)reader.Value) == name)
				{
					if (!reader.Read()
						|| (reader.TokenType != Newtonsoft.Json.JsonToken.StartArray
							&& reader.TokenType != Newtonsoft.Json.JsonToken.Null))
						throw new InvalidOperationException("Expecting array named '" + name + "'!");

					// we skip the deserialization if the array is null
					return reader.TokenType == Newtonsoft.Json.JsonToken.StartArray;
				}
			}

			return false;
		}

		IEnumerator<IViewRow> IEnumerable<IViewRow>.GetEnumerator()
		{
			var response = GetResponse();
			Debug.Assert(response != null);

			using (var sr = new StreamReader(response.GetResponseStream(), Encoding.UTF8, true))
			using (var jsonReader = new JsonTextReader(sr))
			{
				// position the reader on the first "rows" field which contains the actual resultset
				// this way we do not have to deserialize the whole response twice
				if (MoveToArray(jsonReader, 1, "rows"))
				{
					// read until the end of the rows array
					while (jsonReader.Read() && jsonReader.TokenType != Newtonsoft.Json.JsonToken.EndArray)
					{
						var row = new __Row(this, Json.Parse(jsonReader) as IDictionary<string, object>);

						yield return row;
					}
				}

				if (MoveToArray(jsonReader, 1, "errors"))
				{
					var errors = Json.Parse(jsonReader);
					var formatted = String.Join("\n", FormatErrors(errors as object[]).ToArray());
					if (String.IsNullOrEmpty(formatted)) formatted = "<unknown>";

					throw new InvalidOperationException("Cannot read view: " + formatted);
				}
			}
		}

		private static IEnumerable<string> FormatErrors(object[] list)
		{
			if (list == null || list.Length == 0)
				yield break;

			foreach (IDictionary<string, object> error in list)
			{
				string reason;
				string from;

				if (!error.TryGetValue("from", out from)) continue;
				if (!error.TryGetValue("reason", out reason)) continue;

				yield return from + ": " + reason;
			}
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<IViewRow>)this).GetEnumerator();
		}

		/// <summary>
		/// Builds the request uri based on the parameters set by the user
		/// </summary>
		/// <returns></returns>
		private IHttpRequest CreateRequest(IHttpClient client)
		{
			var retval = client.CreateRequest(this.designDocument + "/_view/" + this.indexName);

			AddOptionalRequestParam(retval, "startKey", this.startKey);
			AddOptionalRequestParam(retval, "endKey", this.endKey);
			AddOptionalRequestParam(retval, "startKey_docid", this.startId);
			AddOptionalRequestParam(retval, "endKey_docid", this.endId);
			AddOptionalRequestParam(retval, "skip", this.skip);
			AddOptionalRequestParam(retval, "limit", this.limit);

			AddOptionalRequestParam(retval, "inclusive_end", this.inclusive);
			AddOptionalRequestParam(retval, "descending", this.descending);
			AddOptionalRequestParam(retval, "reduce", this.reduce);
			AddOptionalRequestParam(retval, "group", this.group);
			AddOptionalRequestParam(retval, "group_level", this.groupAt);

			if (this.stale != null)
				switch (this.stale.Value)
				{
					case StaleMode.AllowStale:
						retval.AddParameter("stale", "ok");
						break;
					case StaleMode.UpdateAfter:
						retval.AddParameter("stale", "update_after");
						break;
					default: throw new ArgumentOutOfRangeException("stale: " + this.stale);
				}

			return retval;
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

		#region [ IView                        ]

		IView IView.Limit(int value)
		{
			return new CouchbaseView(this) { limit = value };
		}

		IView IView.Skip(int value)
		{
			return new CouchbaseView(this) { skip = value };
		}

		IView IView.Stale(StaleMode mode)
		{
			return new CouchbaseView(this) { stale = mode };
		}

		IView IView.Descending(bool descending)
		{
			return new CouchbaseView(this) { descending = descending };
		}

		IView IView.StartKey(string from)
		{
			return new CouchbaseView(this) { startKey = from };
		}

		IView IView.EndKey(string to)
		{
			return new CouchbaseView(this) { endKey = to };
		}

		IView IView.StartDocumentId(string from)
		{
			return new CouchbaseView(this) { startId = from };
		}

		IView IView.EndDocumentId(string to)
		{
			return new CouchbaseView(this) { endId = to };
		}

		IView IView.Reduce(bool reduce)
		{
			return new CouchbaseView(this) { reduce = reduce };
		}

		IView IView.Group(bool group)
		{
			return new CouchbaseView(this) { group = group };
		}

		IView IView.GroupAt(int level)
		{
			return new CouchbaseView(this) { groupAt = level };
		}

		IView IView.WithInclusiveEnd(bool inclusive)
		{
			return new CouchbaseView(this) { inclusive = inclusive };
		}

		#endregion
		#region [ Request helpers              ]

		private static void AddOptionalRequestParam(IHttpRequest request, string name, bool? value)
		{
			if (value != null)
				request.AddParameter(name, value.Value ? "true" : "false");
		}

		private static void AddOptionalRequestParam(IHttpRequest request, string name, int? value)
		{
			if (value != null)
				request.AddParameter(name, value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
		}

		private static void AddOptionalRequestParam<T>(IHttpRequest request, string name, T value)
			where T : IConvertible
		{
			if (value != null)
				request.AddParameter(name, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
		}

		#endregion
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
					throw new InvalidOperationException("The value 'id' was not found in the row definition.");

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
				return this.owner.client.Get(this.id);
			}

			IDictionary<string, object> IViewRow.Info
			{
				get { return this.info; }
			}
		}

		#endregion
	}

	public enum StaleMode { AllowStale, UpdateAfter }
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
