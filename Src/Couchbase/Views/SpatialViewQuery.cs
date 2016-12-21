using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Couchbase.Core;
using Couchbase.Utils;

namespace Couchbase.Views
{
    /// <summary>
    /// An implementation for Spatial view request which provide multidimensional spatial indexes in Couchbase.
    /// </summary>
    public class SpatialViewQuery : ISpatialViewQuery
    {
        //[bucket-name]/_design/[design-doc]/_spatial/[spatial-name]
        private const string UriFormat = "{0}://{1}:{2}";
        private const string RelativeUriWithBucket = "{0}/_design/{1}/_spatial/{2}?";
        private const string RelativeUri = "_design/{0}/_spatial/{1}?";

        //uri construction
        private Uri _baseUri;
        private string _designDoc;
        private string _viewName;

        //API parameters
        private StaleState? _stale;
        private int? _skip;
        private int? _limit;
        private List<double?> _startRange;
        private List<double?> _endRange;
        private bool? _development;
        private int? _connectionTimeout;

        //defaults
        private const string DefaultHost = "localhost";
        private const uint DefaultPort = 8092;
        private const uint DefaultSslPort = 18092;
        private const string Http = "http";
        private const string Https = "https";
        private const string DefaultBucket = "default";
        private const string QueryArgPattern = "{0}={1}&";

        private struct QueryArguments
        {
            public const string StartRange = "start_range";
            public const string EndRange = "end_range";
            public const string Bbox = "bbox";
            public const string Limit = "limit";
            public const string Skip = "skip";
            public const string Stale = "stale";
            public const string Timeout = "connection_timeout";
            public const string Development = "development";
        }

        public SpatialViewQuery()
        {
            Port = DefaultPort;
            SslPort = DefaultSslPort;
        }

        public SpatialViewQuery(Uri baseUri)
        {
            _baseUri = baseUri;
        }

        /// <summary>
        /// Sets the base uri for the query if it's not set in the constructor.
        /// </summary>
        /// <param name="uri">The base uri to use - this is normally set internally and may be overridden by configuration.</param>
        /// <returns>An IViewQuery object for chaining</returns>
        /// <remarks>Note that this will override the baseUri set in the ctor. Additionally, this method may be called internally by the <see cref="IBucket"/> and overridden.</remarks>
        IViewQueryable IViewQueryable.BaseUri(Uri uri)
        {
            _baseUri = uri;
            return this;
        }

        /// <summary>
        /// Toogles the if query result to is to be streamed. This is useful for large result sets in that it limits the
        /// working size of the query and helps reduce the possibility of a <see cref="OutOfMemoryException" /> from occurring.
        /// </summary>
        /// <param name="useStreaming">if set to <c>true</c> streams the results as you iterate through the response.</param>
        /// <returns>An IViewQueryable object for chaining</returns>
        public IViewQueryable UseStreaming(bool useStreaming)
        {
            IsStreaming = useStreaming;
            return this;
        }

        /// <summary>
        /// Gets a value indicating if the result should be streamed.
        /// </summary>
        /// <value><c>true</c> if the query result is to be streamed; otherwise, <c>false</c>.</value>
        public bool IsStreaming { get; private set; }

        /// <summary>
        /// The start range of the spatial query.
        /// </summary>
        /// <param name="startRange">The start range.</param>
        /// <returns></returns>
        /// <remarks>
        /// The number of elements must match the number of dimensions of the index
        /// </remarks>
        public ISpatialViewQuery StartRange(params double?[] startRange)
        {
            _startRange = startRange.ToList();
            return this;
        }

        /// <summary>
        /// The start range of the spatial query.
        /// </summary>
        /// <param name="startRange">The start range.</param>
        /// <returns></returns>
        /// <remarks>
        /// The number of elements must match the number of dimensions of the index
        /// </remarks>
        public ISpatialViewQuery StartRange(List<double?> startRange)
        {
            _startRange = startRange;
            return this;
        }

        /// <summary>
        /// The end range of the spatial query.
        /// </summary>
        /// <param name="endRange">The end range.</param>
        /// <returns></returns>
        /// <remarks>
        /// The number of elements must match the number of dimensions of the index
        /// </remarks>
        public ISpatialViewQuery EndRange(List<double?> endRange)
        {
            _endRange = endRange;
            return this;
        }

        /// <summary>
        /// The end range of the spatial query.
        /// </summary>
        /// <param name="endRange">The end range.</param>
        /// <returns></returns>
        /// <remarks>
        /// The number of elements must match the number of dimensions of the index
        /// </remarks>
        public ISpatialViewQuery EndRange(params double?[] endRange)
        {
            _endRange = endRange.ToList();
            return this;
        }

        /// <summary>
        /// The start and end range for a spatial query.
        /// </summary>
        /// <param name="startRange">The start range.</param>
        /// <param name="endRange">The end range.</param>
        /// <returns></returns>
        public ISpatialViewQuery Range(List<double?> startRange, List<double?> endRange)
        {
            StartRange(startRange);
            EndRange(endRange);
            return this;
        }

        /// <summary>
        /// Specifies the design document and view to execute.
        /// </summary>
        /// <param name="designDoc">The design document.</param>
        /// <param name="view">The view.</param>
        /// <returns></returns>
        public ISpatialViewQuery From(string designDoc, string view)
        {
            DesignDoc(designDoc);
            View(view);
            return this;
        }

        /// <summary>
        /// Specifies the name of the bucket to query.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns></returns>
        public ISpatialViewQuery Bucket(string name)
        {
            BucketName = name;
            return this;
        }

        /// <summary>
        /// Specifies the design document.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns></returns>
        public ISpatialViewQuery DesignDoc(string name)
        {
            _designDoc = name;
            return this;
        }

        /// <summary>
        /// Specifies the view to execute.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns></returns>
        public ISpatialViewQuery View(string name)
        {
            _viewName = name;
            return this;
        }

        /// <summary>
        /// Skip this number of records before starting to return the results.
        /// </summary>
        /// <param name="count">The count.</param>
        /// <returns></returns>
        public ISpatialViewQuery Skip(int count)
        {
            _skip = count;
            return this;
        }

        /// <summary>
        /// Specifies the level of data freshness.
        /// </summary>
        /// <param name="staleState">State of the stale.</param>
        /// <returns></returns>
        public ISpatialViewQuery Stale(StaleState staleState)
        {
            _stale = staleState;
            return this;
        }

        /// <summary>
        /// Limit the number of the returned documents to the specified number.
        /// </summary>
        /// <param name="limit">The limit.</param>
        /// <returns></returns>
        public ISpatialViewQuery Limit(int limit)
        {
            _limit = limit;
            return this;
        }

        /// <summary>
        /// Specifies the server timeout.
        /// </summary>
        /// <param name="timeout">The timeout.</param>
        /// <returns></returns>
        public ISpatialViewQuery ConnectionTimeout(int timeout)
        {
            _connectionTimeout = timeout;
            return this;
        }

        /// <summary>
        /// Toggles the query between development or production dataset and View.
        /// </summary>
        /// <param name="development">If true the development View will be used</param>
        /// <returns>An ISpatialViewQuery object for chaining</returns>
        /// <remarks>The default is false; use the published, production view.</remarks>
        public ISpatialViewQuery Development(bool development)
        {
            _development = development;
            return this;
        }

        /// <summary>
        /// Gets the port to use if the default port is overridden.
        /// </summary>
        /// <value>
        /// The port.
        /// </value>
        public uint Port { get; internal set; }

        /// <summary>
        /// Gets the SSL port to use if the default SSL/TLS port is overridden.
        /// </summary>
        /// <value>
        /// The SSL port.
        /// </value>
        public uint SslPort { get; internal set; }

        /// <summary>
        /// Gets the name of the bucket.
        /// </summary>
        /// <value>
        /// The name of the bucket.
        /// </value>
        public string BucketName { get; internal set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use SSL.
        /// </summary>
        /// <value>
        ///   <c>true</c> if [use SSL]; otherwise, <c>false</c>.
        /// </value>
        public bool UseSsl { get; set; }

        /// <summary>
        /// Gets or sets the retry attempts.
        /// </summary>
        /// <value>
        /// The retry attempts.
        /// </value>
        public int RetryAttempts { get; set; }

        /// <summary>
        /// Gets the host that will execute the query.
        /// </summary>
        /// <value>
        /// The host.
        /// </value>
        public string Host { get; internal set; }

        /// <summary>
        /// Raws the URI.
        /// </summary>
        /// <returns></returns>
        public Uri RawUri()
        {
            //for testing...otherwise set internally by the Server class
            if (_baseUri == null)
            {
                var protocol = UseSsl ? Https : Http;
                var port = UseSsl ? SslPort : Port;
                _baseUri = new Uri(string.Format(UriFormat, protocol, DefaultHost, port));
            }
            return new Uri(_baseUri + GetRelativeUri() + GetQueryParams());
        }

        public string GetRelativeUri()
        {
            var view = _viewName;
            var designDoc = _designDoc;
            var bucket = string.IsNullOrWhiteSpace(BucketName) ? DefaultBucket : BucketName;

            if (!string.IsNullOrWhiteSpace(BucketName) &&
                string.IsNullOrWhiteSpace(_baseUri.PathAndQuery) || _baseUri.PathAndQuery.Equals("/"))
            {
                return string.Format(RelativeUriWithBucket, bucket, designDoc, view);
            }
            return string.Format(RelativeUri, designDoc, view);
        }

        public string GetQueryParams()
        {
            var queryParams = new StringBuilder(10);
            if (_stale.HasValue)
            {
                queryParams.AppendFormat(QueryArgPattern, QueryArguments.Stale, _stale.Value.ToLowerString());
            }
            if (_connectionTimeout.HasValue)
            {
                queryParams.AppendFormat(QueryArgPattern, QueryArguments.Timeout, _connectionTimeout.Value);
            }
            if (_limit.HasValue)
            {
                queryParams.AppendFormat(QueryArgPattern, QueryArguments.Limit, _limit);
            }
            if (_skip.HasValue)
            {
                queryParams.AppendFormat(QueryArgPattern, QueryArguments.Skip, _skip);
            }
            if (_startRange != null)
            {
                queryParams.AppendFormat(QueryArgPattern, QueryArguments.StartRange, _startRange.ToJson());
            }
            if (_endRange != null)
            {
                queryParams.AppendFormat(QueryArgPattern, QueryArguments.EndRange, _endRange.ToJson());
            }
            if (_development.HasValue)
            {
                queryParams.AppendFormat(QueryArgPattern, QueryArguments.Development, _development.ToLowerString());
            }
            return queryParams.ToString().TrimEnd('&');
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
