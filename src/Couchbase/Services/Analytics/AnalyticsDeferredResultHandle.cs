using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Couchbase.Core.DataMapping;
using Couchbase.Services.Query;

namespace Couchbase.Services.Analytics
{
    internal class AnalyticsDeferredResultHandle
    {
        public QueryStatus Status { get; set; }
        public string Handle { get; set; }
    }

    internal class AnalyticsDeferredResultHandle<T> : IAnalyticsDeferredResultHandle<T>
    {
        //private static readonly ILog Log = LogManager.GetLogger<AnalyticsDeferredResultHandle<T>>();
        private readonly AnalyticsResult<T> _result;
        private readonly HttpClient _client;
        private readonly IDataMapper _dataMapper;

        internal AnalyticsDeferredResultHandle(AnalyticsResult<T> result, HttpClient client, IDataMapper dataMapper, string handleUri)
        {
            _result = result;
            _client = client;
            _dataMapper = dataMapper;
            HandleUri = handleUri;
        }

        internal string HandleUri;

        /// <summary>
        /// Gets the current status of the deferred query.
        /// NOTE: This is an experimental API and may change in the future.
        /// </summary>
        /// <returns>
        /// The current <see cref="T:Couchbase.N1QL.QueryStatus" /> for the deferred query.
        /// </returns>
        public QueryStatus GetStatus()
        {
            return GetStatusAsync()
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        /// <summary>
        /// Gets the current status of the deferred query asynchronously.
        /// NOTE: This is an experimental API and may change in the future.
        /// </summary>
        /// <returns>
        /// The current <see cref="T:Couchbase.N1QL.QueryStatus" /> for the deferred query.
        /// </returns>
        public async Task<QueryStatus> GetStatusAsync()
        {
            // only check if we know the query is still running
            if (_result.MetaData.Status != QueryStatus.Running)
            {
                return _result.MetaData.Status;
            }

            try
            {
                var result = await _client.GetAsync(HandleUri).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.OK)
                {
                    AnalyticsDeferredResultHandle handle;
                    using (var stream = await result.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    {
                        handle = _dataMapper.Map<AnalyticsDeferredResultHandle>(stream);
                    }

                    HandleUri = handle.Handle;
                    _result.MetaData.Status = handle.Status;
                }
                else
                {
                    ProcessError($"Error trying to get the query status using handle {HandleUri}. {result.StatusCode} - {result.ReasonPhrase}.");
                }
            }
            catch (Exception exception)
            {
                ProcessError($"Error trying to get the query status using handle {HandleUri}. {exception.Message}.");
            }

            return _result.MetaData.Status;
        }

        /// <summary>
        /// Gets the query result for a deferred query.
        /// NOTE: This is an experimental API and may change in the future.
        /// </summary>
        /// <returns>
        /// The query results as a <see cref="T:System.Collections.Generic.IEnumerable`1" />.
        /// </returns>
        public IEnumerable<T> GetRows()
        {
            return GetRowsAsync()
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        /// <summary>
        /// Gets the query result for a deferred query asynchronously.
        /// NOTE: This is an experimental API and may change in the future.
        /// </summary>
        /// <returns>
        /// The query results as a <see cref="T:System.Collections.Generic.IEnumerable`1" />.
        /// </returns>
        public async Task<IEnumerable<T>> GetRowsAsync()
        {
            // if query is not successful or the handle is invalid, we can't get any results
            if (_result.MetaData.Status != QueryStatus.Success || string.IsNullOrWhiteSpace(HandleUri))
            {
                return null;
            }

            try
            {
                var result = await _client.GetAsync(HandleUri).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.OK)
                {
                    List<T> rows;
                    using (var stream = await result.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    {
                        rows = _dataMapper.Map<List<T>>(stream);
                    }

                    return rows;
                }

                ProcessError($"Error when trying to retrieve query results using handle {HandleUri}. {result.StatusCode} - {result.ReasonPhrase}.");
                return null;
            }
            catch (Exception exception)
            {
                ProcessError($"Error when trying to retrieve query results using handle {HandleUri}.", exception);
            }

            return null;
        }

        private void ProcessError(string message, Exception exception = null)
        {
            //Log.Info(message, exception);

            //_result.MetaData.Success = false;
            _result.MetaData.Status = QueryStatus.Fatal;
            //_result.MetaData.Message = message;
            //_result.MetaData.Exception = exception;
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2018 Couchbase, Inc.
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
