using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Enyim.Caching;
using System.Diagnostics;
using System.IO;
using System.Collections;

namespace Couchbase {

    public enum StaleMode { AllowStale, UpdateAfter, False }

    public enum OnErrorMode { Continue, Stop }

    internal abstract class CouchbaseViewBase<T> : IView<T> {

        protected static readonly Enyim.Caching.ILog log = Enyim.Caching.LogManager.GetLogger(typeof(CouchbaseView));

        protected readonly IMemcachedClient client;
        protected readonly IHttpClientLocator clientLocator;
        protected readonly string designDocument;
        protected readonly string indexName;

        protected string endKey;
        protected string startKey;
        protected string endId;
        protected string startId;
        protected string key;
        protected string keys;
        
        protected StaleMode? stale;
        protected OnErrorMode? onError;
        protected bool? descending;
        protected bool? inclusive;

        protected int? skip;
        protected int? limit;

        protected bool? reduce;
        protected bool? group;
        protected int? groupAt;

		public int TotalRows { get; set; }

        internal CouchbaseViewBase(IMemcachedClient client, IHttpClientLocator clientLocator, string designDocument, string indexName) {
            this.client = client;
            this.clientLocator = clientLocator;
            this.designDocument = designDocument;
            this.indexName = indexName;
        }

        protected CouchbaseViewBase(CouchbaseViewBase<T> original) {
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

        protected bool MoveToArray(JsonReader reader, int depth, string name) {
            while (reader.Read()) {
                if (reader.TokenType == Newtonsoft.Json.JsonToken.PropertyName
                    && reader.Depth == depth
                    && ((string)reader.Value) == name) {
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

        protected IEnumerator<T> TransformResults<T>(Func<JsonReader, T> rowTransformer) {
            var response = GetResponse();
            Debug.Assert(response != null);

            using (var sr = new StreamReader(response.GetResponseStream(), Encoding.UTF8, true))
            using (var jsonReader = new JsonTextReader(sr)) {

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

        protected static IEnumerable<string> FormatErrors(object[] list) {
            if (list == null || list.Length == 0)
                yield break;

            foreach (IDictionary<string, object> error in list) {
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
        private IHttpRequest CreateRequest(IHttpClient client) {
            var retval = client.CreateRequest(this.designDocument + "/_view/" + this.indexName);

            AddOptionalRequestParam(retval, "key", this.key);
            AddOptionalRequestParam(retval, "keys", this.keys);
            AddOptionalRequestParam(retval, "startkey", this.startKey);
            AddOptionalRequestParam(retval, "endkey", this.endKey);
            AddOptionalRequestParam(retval, "startkey_docid", this.startId);
            AddOptionalRequestParam(retval, "endkey_docid", this.endId);
            AddOptionalRequestParam(retval, "skip", this.skip);
            AddOptionalRequestParam(retval, "limit", this.limit);

            AddOptionalRequestParam(retval, "inclusive_end", this.inclusive);
            AddOptionalRequestParam(retval, "descending", this.descending);
            AddOptionalRequestParam(retval, "reduce", this.reduce);
            AddOptionalRequestParam(retval, "group", this.group);
            AddOptionalRequestParam(retval, "group_level", this.groupAt);

            if (this.stale != null)
                switch (this.stale.Value) {
                    case StaleMode.AllowStale:
                        retval.AddParameter("stale", "ok");
                        break;
                    case StaleMode.UpdateAfter:
                        retval.AddParameter("stale", "update_after");
                        break;
                    case StaleMode.False:
                        retval.AddParameter("stale", "false");
                        break;
                    default: throw new ArgumentOutOfRangeException("stale: " + this.stale);
                }

            if (this.onError != null)
            {
                switch (onError.Value) {
                    case OnErrorMode.Continue:
                        retval.AddParameter("on_error", "continue");
                        break;
                    case OnErrorMode.Stop:
                        retval.AddParameter("on_error", "stop");
                        break;
                    default: throw new ArgumentOutOfRangeException("on_error: " + this.onError);
                }
            }

            return retval;
        }

        protected IHttpResponse GetResponse() {
            Debug.Assert(this.clientLocator != null);

            var client = this.clientLocator.Locate(this.designDocument);
            if (client == null) {
                if (log.IsErrorEnabled)
                    log.WarnFormat("View {0} was mapped to a dead node, failing.", this);

                throw new InvalidOperationException();
            }

            var request = CreateRequest(client);

            return request.GetResponse();
        }

        #region [ Request helpers              ]

        private static void AddOptionalRequestParam(IHttpRequest request, string name, bool? value) {
            if (value != null)
                request.AddParameter(name, value.Value ? "true" : "false");
        }

        private static void AddOptionalRequestParam(IHttpRequest request, string name, int? value) {
            if (value != null)
                request.AddParameter(name, value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        private static void AddOptionalRequestParam<T>(IHttpRequest request, string name, T value)
            where T : IConvertible {
            if (value != null)
                request.AddParameter(name, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        #endregion

        #region [ IView                        ]

        public IView<T> Limit(int value) {

            this.limit = value;
            return this;
        }

        public IView<T> Skip(int value) {
            this.skip = value;
            return this;
        }

        public IView<T> OnError(OnErrorMode mode) {
            this.onError = mode;
            return this;
        }

        public IView<T> Stale(StaleMode mode) {
            this.stale = mode;
            return this;
        }

        public IView<T> Descending(bool descending) {
            this.descending = descending;
            return this;
        }

        public IView<T> Key<KeyType>(KeyType key) {
            this.key = formatKey<KeyType>(key);
            return this;
        }

        public IView<T> Keys<KeyType>(KeyType keys) {
            this.keys = formatKey<KeyType>(keys);
            return this;
        }

        public IView<T> StartKey<KeyType>(KeyType from) {
            this.startKey = formatKey<KeyType>(from);
            return this;
        }

        public IView<T> EndKey<KeyType>(KeyType to) {
            this.endKey = formatKey<KeyType>(to);
            return this;
        }        

        public IView<T> StartDocumentId(string from) {
            this.startId = from;
            return this;
        }

        public IView<T> EndDocumentId(string to) {
            this.endId = to;
            return this;
        }

        public IView<T> Reduce(bool reduce) {
            this.reduce = reduce;
            return this;
        }

        public IView<T> Group(bool group) {
            this.group = group;
            return this;
        }

        public IView<T> GroupAt(int level) {
            this.groupAt = level;
            return this;
        }

        public IView<T> WithInclusiveEnd(bool inclusive) {
            this.inclusive = inclusive;
            return this;
        }

        public IPagedView<T> GetPagedView(int pageSize, string pagedViewIdProperty = null, string pagedViewKeyProperty = null) {
            return new PagedView<T>(this, pageSize, pagedViewIdProperty, pagedViewKeyProperty);
        }

        #endregion     

        #region IEnumerable<IViewRow> Members

        public abstract IEnumerator<T> GetEnumerator();

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator() {
            throw new NotImplementedException();
        }

        #endregion

        private string formatKey<KeyType>(KeyType key) {

			return JsonConvert.SerializeObject(key);
        }
    }
}
