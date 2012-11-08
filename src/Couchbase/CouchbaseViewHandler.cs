using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;

namespace Couchbase
{
	internal class CouchbaseViewHandler
	{
		protected static readonly Enyim.Caching.ILog log = Enyim.Caching.LogManager.GetLogger(typeof(CouchbaseViewHandler));

		public ICouchbaseClient Client { get; private set; }
		public IHttpClientLocator ClientLocator { get; private set; }
		public string DesignDocument { get; private set; }
		public string ViewPath { get; private set; }
		public string IndexName { get; private set; }

		internal CouchbaseViewHandler(ICouchbaseClient client, IHttpClientLocator clientLocator, string designDocument, string indexName, string viewPath = "_view")
		{
            this.Client = client;
            this.ClientLocator = clientLocator;
            this.DesignDocument = designDocument;
            this.IndexName = indexName;
			this.ViewPath = viewPath;
        }

		public int TotalRows { get; set; }

		public bool MoveToArray(JsonReader reader, int depth, string name)
		{
			while (reader.Read())
			{
				if (reader.TokenType == Newtonsoft.Json.JsonToken.PropertyName
					&& reader.Depth == depth
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

		public IEnumerator<T> TransformResults<T>(Func<JsonReader, T> rowTransformer, IDictionary<string, string> viewParams)
		{
			var response = GetResponse(viewParams);
			Debug.Assert(response != null);

			using (var sr = new StreamReader(response.GetResponseStream(), Encoding.UTF8, true))
			using (var jsonReader = new JsonTextReader(sr))
			{

				while (jsonReader.Read())
				{
					if (jsonReader.TokenType == JsonToken.PropertyName && jsonReader.Depth == 1)
					{
						if (jsonReader.Value as string == "total_rows" && jsonReader.Read())
						{
							TotalRows = Convert.ToInt32(jsonReader.Value);
						}
						else if (jsonReader.Value as string == "rows" && jsonReader.Read())
						{
							// position the reader on the first "rows" field which contains the actual resultset
							// this way we do not have to deserialize the whole response twice
							// read until the end of the rows array
							while (jsonReader.Read() && jsonReader.TokenType != Newtonsoft.Json.JsonToken.EndArray)
							{
								var row = rowTransformer(jsonReader);
								yield return row;
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
				}
			}
		}

		protected static IEnumerable<string> FormatErrors(object[] list)
		{
			if (list == null || list.Length == 0)
				yield break;

			foreach (IDictionary<string, object> error in list)
			{
				object reason;
				object from;

				if (!error.TryGetValue("from", out from)) continue;
				if (!error.TryGetValue("reason", out reason)) continue;

				yield return from + ": " + reason;
			}
		}

		/// <summary>
		/// Builds the request uri based on the parameters set by the user
		/// </summary>
		/// <returns></returns>
		public IHttpRequest CreateRequest(IHttpClient client, Dictionary<string, string> viewParams = null)
		{
			var retval = client.CreateRequest(this.DesignDocument + "/" + this.ViewPath + "/" + this.IndexName);
			return retval;
		}

		public IHttpResponse GetResponse(IDictionary<string, string> viewParams)
		{
			Debug.Assert(this.ClientLocator != null);

			var client = this.ClientLocator.Locate(this.DesignDocument);
			if (client == null)
			{
				if (log.IsErrorEnabled)
					log.WarnFormat("View {0} was mapped to a dead node, failing.", this);

				throw new InvalidOperationException();
			}

			var request = CreateRequest(client);

			if (viewParams != null)
			{
				foreach (var param in viewParams)
				{
					request.AddParameter(param.Key, param.Value.ToString());
				}
			}

			return request.GetResponse();
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