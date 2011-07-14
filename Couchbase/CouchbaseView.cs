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

namespace Couchbase
{
	/// <summary>
	/// Represents the results of a Couchbase index.
	/// </summary>
	public class CouchbaseView : ICouchbaseView
	{
		private static readonly Enyim.Caching.ILog log = Enyim.Caching.LogManager.GetLogger(typeof(CouchbaseView));

		private readonly CouchbaseClient ownerClient;
		private readonly string designDocument;
		private readonly string indexName;

		private string endKey;
		private string startKey;
		private bool? descending;
		private int? skip;
		private int? limit;
		private bool stale;

		internal CouchbaseView(CouchbaseClient ownerClient, string designDocument, string indexName)
		{
			this.ownerClient = ownerClient;
			this.designDocument = designDocument;
			this.indexName = indexName;
		}

		protected CouchbaseView(CouchbaseView original)
		{
			this.ownerClient = original.ownerClient;
			this.designDocument = original.designDocument;
			this.indexName = original.indexName;

			this.endKey = original.endKey;
			this.startKey = original.startKey;
			this.descending = original.descending;
			this.skip = original.skip;
			this.limit = original.limit;
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
		/// <returns></returns>
		ICouchbaseView ICouchbaseView.Range(string from, string to)
		{
			return new CouchbaseView(this)
			{
				startKey = from,
				endKey = to
			};
		}

		/// <summary>
		/// Builds the request uri based on the parameters set by the user
		/// </summary>
		/// <returns></returns>
		private Hammock.RestRequest CreateRequest()
		{
			var retval = new RestRequest
			{
				Path = this.designDocument + "/_view/" + this.indexName,
				Method = Hammock.Web.WebMethod.Get
			};

			if (this.endKey != null) retval.AddParameter("endKey", this.endKey);
			if (this.startKey != null) retval.AddParameter("startKey", this.startKey);
			if (this.descending != null) retval.AddParameter("descending", this.descending.Value ? "true" : "false");
			if (this.skip != null) retval.AddParameter("skip", this.skip.ToString());
			if (this.limit != null) retval.AddParameter("limit", this.limit.ToString());
			if (this.stale) retval.AddParameter("stale", "ok");

			return retval;
		}

		public override string ToString()
		{
			return this.designDocument + "/" + this.indexName;
		}

		private RestClient GetRestClient()
		{
			// find the node hosting this design document
			var pool = ((Enyim.Caching.Memcached.IServerPool)this.ownerClient.PoolInstance);
			var node = pool.Locate(this.designDocument) as CouchbaseNode;

			// return null if the node is dead
			return (node != null && node.IsAlive)
					? node.Client
					: null;
		}

		private RestResponse GetResponse()
		{
			Debug.Assert(this.ownerClient != null);

			var client = this.GetRestClient();
			if (client == null)
			{
				if (log.IsErrorEnabled)
					log.WarnFormat("View {0} was mapped to a dead node, failing.", this);

				throw new InvalidOperationException();
			}

			var request = CreateRequest();
			var response = client.Request(request);

			if (response.InnerException != null) throw response.InnerException;
			if (response.StatusCode != System.Net.HttpStatusCode.OK)
				throw new InvalidOperationException(String.Format("Server returned {0}: {1}, {2}", response.StatusCode, response.StatusDescription, response.Content));

			return response;
		}

		IEnumerator<ICouchbaseViewRow> IEnumerable<ICouchbaseViewRow>.GetEnumerator()
		{
			var response = GetResponse();
			Debug.Assert(response != null);

			using (var sr = new StreamReader(response.ContentStream, Encoding.UTF8, true))
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

				var serializer = new JsonSerializer();

				// read until the end of the rows array
				while (jsonReader.Read() && jsonReader.TokenType != Newtonsoft.Json.JsonToken.EndArray)
				{
					var row = new __Row(this);

					serializer.Populate(jsonReader, row);

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
			private CouchbaseView owner;

			public __Row(CouchbaseView owner)
			{
				this.owner = owner;
			}

			// these will be set by the deserializer
#pragma warning disable 0649
			[JsonProperty("key")]
			public string key;
			[JsonProperty("id")]
			public string id;
			[JsonProperty("value")]
			public JObject info;
#pragma warning restore 0649

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
				return this.owner.ownerClient.Get(this.id);
			}

			JObject ICouchbaseViewRow.Info
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
