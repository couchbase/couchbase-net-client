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

    internal abstract class CouchbaseViewBase<T> : IView<T> {

        protected static readonly Enyim.Caching.ILog log = Enyim.Caching.LogManager.GetLogger(typeof(CouchbaseView));

		protected readonly CouchbaseViewHandler ViewHandler;

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

		protected bool? debug;
        private bool _urlEncode;

        public int TotalRows {
			get { return ViewHandler.TotalRows; }
		}

		public IDictionary<string, object> DebugInfo {
			get { return ViewHandler.DebugInfo; }
		}

        internal CouchbaseViewBase(ICouchbaseClient client, IHttpClientLocator clientLocator, string designDocument, string indexName, int retryCount)
        {
            _urlEncode = false;
            this.ViewHandler = new CouchbaseViewHandler(client, clientLocator, designDocument, indexName, retryCount);
            ViewHandler.UrlEncode = _urlEncode;
        }

        protected CouchbaseViewBase(CouchbaseViewBase<T> original) {
            _urlEncode = false;
            this.ViewHandler = original.ViewHandler;
            ViewHandler.UrlEncode = _urlEncode;

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

			this.debug = original.debug;
        }

        protected IEnumerator<T> TransformResults<T>(Func<JsonReader, T> rowTransformer)
        {
			var viewParamsBuilder = new ViewParamsBuilder();
			viewParamsBuilder.AddOptionalParam("key", this.key);
			viewParamsBuilder.AddOptionalParam("keys", this.keys);
			viewParamsBuilder.AddOptionalParam("startkey", this.startKey);
			viewParamsBuilder.AddOptionalParam("endkey", this.endKey);
			viewParamsBuilder.AddOptionalParam("startkey_docid", this.startId);
			viewParamsBuilder.AddOptionalParam("endkey_docid", this.endId);
			viewParamsBuilder.AddOptionalParam("inclusive_end", this.inclusive);
			viewParamsBuilder.AddOptionalParam("descending", this.descending);
			viewParamsBuilder.AddOptionalParam("reduce", this.reduce);
			viewParamsBuilder.AddOptionalParam("group", this.group);
			viewParamsBuilder.AddOptionalParam("group_level", this.groupAt);
			viewParamsBuilder.AddOptionalParam("skip", this.skip);
			viewParamsBuilder.AddGreaterThanOneParam("limit", this.limit);
			viewParamsBuilder.AddStaleParam(this.stale);
			viewParamsBuilder.AddOnErrorParam(this.onError);
			viewParamsBuilder.AddOptionalParam("debug", this.debug);
            ViewHandler.UrlEncode = _urlEncode;

			return this.ViewHandler.TransformResults<T>(rowTransformer, viewParamsBuilder.Build());
        }

        #region [ IView                        ]

        public IView<T> UrlEncode(bool value)
        {
            _urlEncode = value;
            return this;
        }

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

		public IView<T> Debug(bool debug)
		{
			this.debug = debug;
			return this;
		}

        public IPagedView<T> GetPagedView(int pageSize, string pagedViewIdProperty = null, string pagedViewKeyProperty = null) {
            return new PagedView<T>(this, pageSize, pagedViewIdProperty, pagedViewKeyProperty);
        }

		public bool CheckExists()
		{
			return ViewHandler.CheckViewExists();
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
