using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.Utils;

namespace Couchbase.Views
{
    ////http://192.168.56.101:8091/couchBase/Trades/_design/dev_test/_view/test?stale=false&connection_timeout=60000&limit=10&skip=0&_=1395208080725
    public class ViewQuery : IViewQuery
    {
        private readonly ILog Log = LogManager.GetCurrentClassLogger();
        public const string CouchbaseApi = "couchBase";
        public const string Design = "_design";
        public const string DevelopmentViewPrefix = "dev_";
        public const string ViewMethod = "_view";
        public const string ForwardSlash = "/";
        public const string QueryOperator = "?";
        const string QueryArgPattern = "{0}={1}&";
        private const string DefaultHost = "http://localhost:8091/";

        private string _baseUri;
        private string _bucketName;
        private string _designDoc;
        private string _viewName;
        private bool? _development;
        private int? _skipCount;
        private StaleState _staleState;
        private bool? _descending;
        private object _endKey;
        private object _endDocId;
        private bool? _fullSet;
        private bool? _group;
        private int? _groupLevel;
        private bool? _inclusiveEnd;
        private object _key;
        private IEnumerable _keys;
        private int? _limit;
        private bool? _continueOnError;
        private bool? _reduce;
        private object _startKey;
        private object _startKeyDocId;
        private int? _connectionTimeout;

        private struct QueryArguments
        {
            public const string Descending = "descending";
            public const string EndKey = "endkey";
            public const string EndKeyDocId = "endkey_docid";
            public const string FullSet = "full_set";
            public const string Group = "group";
            public const string GroupLevel = "group_level";
            public const string InclusiveEnd = "inclusive_end";
            public const string Key = "key";
            public const string Keys = "keys";
            public const string Limit = "limit";
            public const string OnError = "on_error";
            public const string Reduce = "reduce";
            public const string Skip = "skip";
            public const string Stale = "stale";
            public const string StartKey = "startkey";
            public const string StartKeyDocId = "startkey_docid";
            public const string ConnectionTimeout = "connection_timeout";
        }

        public ViewQuery(bool development)
            : this(DefaultHost, null, development)
        {
        }

        public ViewQuery(string baseUri, bool development) 
            : this(baseUri, null, development)
        {
        }

        public ViewQuery(string baseUri, string designDoc, bool development) 
            : this(baseUri, designDoc, null, development)
        {
        }
         
        public ViewQuery(string baseUri, string designDoc, string viewName, bool development)
        {
            _baseUri = baseUri;
            _designDoc = designDoc;
            _viewName = viewName;
            _development = development;
        }

        public IViewQuery From(string bucketName, string designDoc)
        {
            return Bucket(bucketName).DesignDoc(designDoc);
        }

        public IViewQuery Bucket(string name)
        {
            _bucketName = name;
            return this;
        }

        public IViewQuery DesignDoc(string name)
        {
            _designDoc = name;
            return this;
        }

        public IViewQuery View(string name)
        {
            _viewName = name;
            return this;
        }

        public IViewQuery Skip(int count)
        {
            _skipCount = count;
            return this;
        }

        public IViewQuery Stale(StaleState staleState)
        {
            _staleState = staleState;
            return this;
        }

        public IViewQuery Asc()
        {
            _descending = false;
            return this;
        }

        public IViewQuery Desc()
        {
            _descending = true;
            return this;
        }

        public IViewQuery EndKey(object endKey)
        {
            _endKey = endKey;
            return this;
        }

        public IViewQuery EndKeyDocId(int docId)
        {
            _endDocId = docId;
            return this;
        }

        public IViewQuery FullSet()
        {
            _fullSet = true;
            return this;
        }

        public IViewQuery Group(bool group)
        {
            _group = group;
            return this;
        }

        public IViewQuery GroupLevel(int level)
        {
            _groupLevel = level;
            return this;
        }

        public IViewQuery InclusiveEnd(bool inclusiveEnd)
        {
            _inclusiveEnd = inclusiveEnd;
            return this;
        }

        public IViewQuery Key(object key)
        {
            _key = key;
            return this;
        }

        public IViewQuery Keys(IEnumerable keys)
        {
            _keys = keys;
            return this;
        }

        public IViewQuery Limit(int limit)
        {
            _limit = limit;
            return this;
        }

        public IViewQuery OnError(bool stop)
        {
            _continueOnError = stop;
            return this;
        }

        public IViewQuery Reduce(bool reduce)
        {
            _reduce = reduce;
            return this;
        }

        public IViewQuery StartKey(object startKey)
        {
            _startKey = startKey;
            return this;
        }

        public IViewQuery StartKeyDocId(object docId)
        {
            _startKeyDocId = docId;
            return this;
        }

        public IViewQuery ConnectionTimeout(int timeout)
        {
            _connectionTimeout = timeout;
            return this;
        }

        public Uri RawUri()
        {
            var sb = new StringBuilder();
            sb.Append(_baseUri);

            if (!_baseUri.EndsWith(ForwardSlash))
            {
                sb.Append(ForwardSlash);
            }

            sb.Append(CouchbaseApi);
            sb.Append(ForwardSlash);
            sb.Append(_bucketName);
            sb.Append(ForwardSlash);
            sb.Append(Design);
            sb.Append(ForwardSlash);

            if (_development.HasValue && _development.Value)
            {
                sb.Append(DevelopmentViewPrefix);
            }

            sb.Append(_designDoc);
            sb.Append(ForwardSlash);
            sb.Append(ViewMethod);
            sb.Append(ForwardSlash);
            sb.Append(_viewName);
            sb.Append(QueryOperator);

            if (_staleState != StaleState.None)
            {
                sb.AppendFormat(QueryArgPattern, QueryArguments.Stale, _staleState.ToLowerString());
            }
            if (_descending.HasValue)
            {
                sb.AppendFormat(QueryArgPattern, QueryArguments.Descending, _descending.ToLowerString());
            }
            if (_continueOnError.HasValue)
            {
                sb.AppendFormat(QueryArgPattern, QueryArguments.OnError, _continueOnError.Value ? "continue" : "stop");
            }
            if (_endDocId != null)
            {
                sb.AppendFormat(QueryArgPattern, QueryArguments.EndKeyDocId, _endDocId);
            }
            if (_endKey != null)
            {
                sb.AppendFormat(QueryArgPattern, QueryArguments.EndKey, _endKey);
            }
            if (_fullSet.HasValue)
            {
                sb.AppendFormat(QueryArgPattern, QueryArguments.FullSet, _fullSet.ToLowerString());
            }
            if (_group.HasValue)
            {
                sb.AppendFormat(QueryArgPattern, QueryArguments.Group, _group.ToLowerString());
            }
            if (_groupLevel.HasValue)
            {
                sb.AppendFormat(QueryArgPattern, QueryArguments.GroupLevel, _groupLevel);
            }
            if (_inclusiveEnd.HasValue)
            {
                sb.AppendFormat(QueryArgPattern, QueryArguments.InclusiveEnd, _inclusiveEnd.ToLowerString());
            }
            if (_key != null)
            {
                sb.AppendFormat(QueryArgPattern, QueryArguments.Key, _key);
            }
            if (_keys != null)
            {
                sb.AppendFormat(QueryArgPattern, QueryArguments.Keys, _keys);
            }
            if (_limit.HasValue)
            {
                sb.AppendFormat(QueryArgPattern, QueryArguments.Limit, _limit);
            }
            if (_reduce.HasValue)
            {
                sb.AppendFormat(QueryArgPattern, QueryArguments.Reduce, _reduce.ToLowerString());
            }
            if (_startKey != null)
            {
                sb.AppendFormat(QueryArgPattern, QueryArguments.StartKey, _startKey);
            }
            if (_startKeyDocId != null)
            {
                sb.AppendFormat(QueryArgPattern, QueryArguments.StartKeyDocId, _startKeyDocId);
            }
            if (_skipCount.HasValue)
            {
                sb.AppendFormat(QueryArgPattern, QueryArguments.Skip, _skipCount);
            }
            if (_connectionTimeout.HasValue)
            {
                sb.AppendFormat(QueryArgPattern, QueryArguments.ConnectionTimeout, _connectionTimeout);
            }

            var requestUri = sb.ToString().TrimEnd('&');
            Log.Debug(m=>m(requestUri));
            return new Uri(requestUri);
        }
    }
}
