using System;
using System.Collections;
using System.Text;
using Common.Logging;
using Couchbase.Core;
using Couchbase.Utils;

namespace Couchbase.Views
{
    /// <summary>
    /// Implemented as an object that can query a Couchbase View.
    /// </summary>
    public class ViewQuery : IViewQuery
    {
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();
        public const string CouchbaseApi = "couchBase";
        public const string Design = "_design";
        public const string DevelopmentViewPrefix = "dev_";
        public const string ViewMethod = "_view";
        public const string ForwardSlash = "/";
        public const string QueryOperator = "?";
        const string QueryArgPattern = "{0}={1}&";
        private const string DefaultHost = "http://localhost:8092/";

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
            : this(null, DefaultHost, development)
        {
        }

        public ViewQuery(string baseUri, bool development)
            : this(null, baseUri, development)
        {
        }

        public ViewQuery(string bucketName, string baseUri, bool development)
            : this(bucketName, baseUri, null, development)
        {
        }

        public ViewQuery(string bucketName, string baseUri, string designDoc, bool development)
            : this(bucketName, baseUri, designDoc, null, development)
        {
        }

        public ViewQuery(string bucketName, string baseUri, string designDoc, string viewName, bool development)
        {
            _bucketName = bucketName;
            _baseUri = baseUri;
            _designDoc = designDoc;
            _viewName = viewName;
            _development = development;
        }

        /// <summary>
        /// Specifies the bucket and design document to target for a query.
        /// </summary>
        /// <param name="bucketName">The bucket to target</param>
        /// <param name="designDoc">The design document to use</param>
        /// <returns></returns>
        public IViewQuery From(string bucketName, string designDoc)
        {
            return Bucket(bucketName).DesignDoc(designDoc);
        }

        /// <summary>
        /// Sets the name of the Couchbase Bucket.
        /// </summary>
        /// <param name="name">The name of the bucket.</param>
        /// <returns>An IViewQuery object for chaining</returns>
        public IViewQuery Bucket(string name)
        {
            _bucketName = name;
            return this;
        }

        /// <summary>
        /// Sets the name of the design document.
        /// </summary>
        /// <param name="name">The name of the design document to use.</param>
        /// <returns>An IViewQuery object for chaining</returns>
        public IViewQuery DesignDoc(string name)
        {
            _designDoc = name;
            return this;
        }

        /// <summary>
        /// Sets the name of the view to query.
        /// </summary>
        /// <param name="name">The name of the view to query.</param>
        /// <returns>An IViewQuery object for chaining</returns>
        public IViewQuery View(string name)
        {
            _viewName = name;
            return this;
        }

        /// <summary>
        /// Skip this number of records before starting to return the results
        /// </summary>
        /// <param name="count">The number of records to skip</param>
        /// <returns></returns>
        public IViewQuery Skip(int count)
        {
            _skipCount = count;
            return this;
        }

        /// <summary>
        /// Allow the results from a stale view to be used. The default is StaleState.Ok; for development work set to StaleState.False
        /// </summary>
        /// <param name="staleState">The staleState value to use.</param>
        /// <returns>An IViewQuery object for chaining</returns>
        public IViewQuery Stale(StaleState staleState)
        {
            _staleState = staleState;
            return this;
        }

        /// <summary>
        /// Return the documents in ascending by key order
        /// </summary>
        /// <returns>An IViewQuery object for chaining</returns>
        public IViewQuery Asc()
        {
            _descending = false;
            return this;
        }

        /// <summary>
        /// Return the documents in descending by key order
        /// </summary>
        /// <returns>An IViewQuery object for chaining</returns>
        public IViewQuery Desc()
        {
            _descending = true;
            return this;
        }

        /// <summary>
        /// Stop returning records when the specified key is reached. Key must be specified as a JSON value.
        /// </summary>
        /// <param name="endKey">The key to stop at</param>
        /// <returns>An IViewQuery object for chaining</returns>
        public IViewQuery EndKey(object endKey)
        {
            _endKey = endKey;
            return this;
        }

        /// <summary>
        /// Stop returning records when the specified document ID is reached
        /// </summary>
        /// <param name="docId">The document Id to stop at.</param>
        /// <returns>An IViewQuery object for chaining</returns>
        public IViewQuery EndKeyDocId(int docId)
        {
            _endDocId = docId;
            return this;
        }

        /// <summary>
        /// Use the full cluster data set (development views only).
        /// </summary>
        /// <returns>An IViewQuery object for chaining</returns>
        public IViewQuery FullSet()
        {
            _fullSet = true;
            return this;
        }

        /// <summary>
        /// Group the results using the reduce function to a group or single row
        /// </summary>
        /// <param name="group">True to group using the reduce function into a single row</param>
        /// <returns>An IViewQuery object for chaining</returns>
        public IViewQuery Group(bool group)
        {
            _group = group;
            return this;
        }

        /// <summary>
        /// Specify the group level to be used
        /// </summary>
        /// <param name="level">The level of grouping to use</param>
        /// <returns>An IViewQuery object for chaining</returns>
        public IViewQuery GroupLevel(int level)
        {
            _groupLevel = level;
            return this;
        }

        /// <summary>
        /// Specifies whether the specified end key should be included in the result
        /// </summary>
        /// <param name="inclusiveEnd">True to include the last key in the result</param>
        /// <returns>An IViewQuery object for chaining</returns>
        public IViewQuery InclusiveEnd(bool inclusiveEnd)
        {
            _inclusiveEnd = inclusiveEnd;
            return this;
        }

        /// <summary>
        /// Return only documents that match the specified key. Key must be specified as a JSON value.
        /// </summary>
        /// <param name="key">The key to retrieve</param>
        /// <returns>An IViewQuery object for chaining</returns>
        public IViewQuery Key(object key)
        {
            _key = key;
            return this;
        }

        /// <summary>
        /// Return only documents that match each of keys specified within the given array. Key must be specified as a JSON value. Sorting is not applied when using this option.
        /// </summary>
        /// <param name="keys">The keys to retrieve</param>
        /// <returns>An IViewQuery object for chaining</returns>
        public IViewQuery Keys(IEnumerable keys)
        {
            _keys = keys;
            return this;
        }

        /// <summary>
        /// Limit the number of the returned documents to the specified number
        /// </summary>
        /// <param name="limit">The numeric limit</param>
        /// <returns>An IViewQuery object for chaining</returns>
        public IViewQuery Limit(int limit)
        {
            _limit = limit;
            return this;
        }

        /// <summary>
        /// Sets the response in the event of an error
        /// </summary>
        /// <param name="stop">True to stop in the event of an error; true to continue</param>
        /// <returns>An IViewQuery object for chaining</returns>
        public IViewQuery OnError(bool stop)
        {
            _continueOnError = stop;
            return this;
        }

        /// <summary>
        /// Use the reduction function
        /// </summary>
        /// <param name="reduce">True to use the reduduction property. Default is false;</param>
        /// <returns>An IViewQuery object for chaining</returns>
        public IViewQuery Reduce(bool reduce)
        {
            _reduce = reduce;
            return this;
        }

        /// <summary>
        /// Return records with a value equal to or greater than the specified key. Key must be specified as a JSON value.
        /// </summary>
        /// <param name="startKey">The key to return records greater than or equal to.</param>
        /// <returns>An IViewQuery object for chaining</returns>
        public IViewQuery StartKey(object startKey)
        {
            _startKey = startKey;
            return this;
        }

        /// <summary>
        /// Return records starting with the specified document ID.
        /// </summary>
        /// <param name="docId">The docId to return records greater than or equal to.</param>
        /// <returns>An IViewQuery object for chaining</returns>
        public IViewQuery StartKeyDocId(object docId)
        {
            _startKeyDocId = docId;
            return this;
        }

        /// <summary>
        /// The number of seconds before the request will be terminated if it has not completed.
        /// </summary>
        /// <param name="timeout">The period of time in seconds</param>
        /// <returns></returns>
        public IViewQuery ConnectionTimeout(int timeout)
        {
            _connectionTimeout = timeout;
            return this;
        }

        /// <summary>
        /// Sets the base uri for the query if it's not set in the constructor.
        /// </summary>
        /// <param name="uri">The base uri to use - this is normally set internally and may be overridden by configuration.</param>
        /// <returns>An IViewQuery object for chaining</returns>
        /// <remarks>Note that this will override the baseUri set in the ctor. Additionally, this method may be called internally by the <see cref="IBucket"/> and overridden.</remarks>
        public IViewQuery BaseUri(string uri)
        {
            _baseUri = uri;
            return this;
        }

        /// <summary>
        /// Gets the name of the <see cref="IBucket"/> that the query is targeting.
        /// </summary>
        public string BucketName
        {
            get { return _bucketName; }
        }

        /// <summary>
        /// Returns the raw REST URI which can be executed in a browser or using curl.
        /// </summary>
        /// <returns></returns>
        public Uri RawUri()
        {
            var sb = new StringBuilder();
            sb.Append(_baseUri);

            if (!_baseUri.EndsWith(ForwardSlash))
            {
                sb.Append(ForwardSlash);
            }

            if (!string.IsNullOrWhiteSpace(_bucketName) && !_baseUri.Contains(_bucketName))
            {
                sb.Append(_bucketName);
                sb.Append(ForwardSlash);
            }
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

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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