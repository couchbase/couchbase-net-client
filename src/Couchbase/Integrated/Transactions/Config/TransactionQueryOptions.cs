#nullable enable
using System;
using System.Collections.Generic;
using Couchbase.Core.Compatibility;
using Couchbase.Core.IO.Serializers;
using Couchbase.Query;

namespace Couchbase.Integrated.Transactions.Config
{
    /// <summary>
    /// A limited subset of <see cref="QueryOptions"/> that are usable in Transactions.
    /// </summary>
    [InterfaceStability(Level.Volatile)]
    public class
        TransactionQueryOptions
    {
        // TODO: change this to an immutable record.
        // if you add anything to this section, add a corresponding 'if' block to the Build() method.
        private readonly Dictionary<string, object> _parameters = new();
        private readonly List<object> _arguments = new();
        private readonly Dictionary<string, object> _rawParameters = new();
        private QueryScanConsistency? _scanConsistency = null;
        private bool? _flexIndex = null;
        private string? _clientContextId = null;
        private TimeSpan? _scanWait = null;
        private int? _scanCap = null;
        private int? _pipelineBatch = null;
        private int? _pipelineCap = null;
        private bool? _readonly = null;
        private bool? _adhoc = null;
        private TimeSpan? _timeout = null;
        private ITypeSerializer? _serializer = null;

        /// <summary>
        /// Build a new instance of QueryOptions based on these TransactionQueryOptions.
        /// </summary>
        /// <param name="txImplicit">Whether this is a single query transaction.</param>
        /// <returns>A new instance of QueryOptions</returns>
        internal QueryOptions Build(bool txImplicit)
        {
            QueryOptions opts = new();
            foreach (var kvp in _parameters)
            {
                opts.Parameter(kvp.Key, kvp.Value);
            }

            foreach (var arg in _arguments)
            {
                opts.Parameter(arg);
            }

            foreach (var raw in _rawParameters)
            {
                opts.Raw(raw.Key, raw.Value);
            }

            if (_scanConsistency != null)
            {
                opts.ScanConsistency(_scanConsistency.Value);
            }
            else if (txImplicit)
            {
                // Due to MB-50914, query is not setting the expected scan consistency level of request_plus on some server versions.
                // So we set it client-side if the user has not specified a scan consistency.
                opts.ScanConsistency(QueryScanConsistency.RequestPlus);
            }

            if (_flexIndex != null)
            {
                opts.FlexIndex(_flexIndex.Value);
            }

            if (_clientContextId != null)
            {
                opts.ClientContextId(_clientContextId);
            }

            if (_scanWait != null)
            {
                opts.ScanWait(_scanWait.Value);
            }

            if (_scanCap != null)
            {
                opts.ScanCap(_scanCap.Value);
            }

            if (_pipelineBatch != null)
            {
                opts.PipelineBatch(_pipelineBatch.Value);
            }

            if (_pipelineCap != null)
            {
                opts.PipelineCap(_pipelineCap.Value);
            }

            if (_readonly != null)
            {
                opts.Readonly(_readonly.Value);
            }

            if (_adhoc != null)
            {
                opts.AdHoc(_adhoc.Value);
            }

            if (_timeout != null)
            {
                opts.Timeout(_timeout.Value);
            }

            if (_serializer != null)
            {
                opts.Serializer = _serializer;
            }

            return opts;
        }

        /// <summary>
        /// Set a parameter by key and value.
        /// </summary>
        /// <param name="key">The key of the parameter.</param>
        /// <param name="val">The value of the parameter.</param>
        /// <returns>The same TransactionsQueryOptions instance.</returns>
        public TransactionQueryOptions Parameter(string key, object val)
        {
            _parameters.Add(key, val);
            return this;
        }

        /// <summary>
        /// Sets a positional parameter for this query.
        /// </summary>
        /// <param name="paramValue">The value to set.</param>
        /// <returns>The same TransactionsQueryOptions instance.</returns>
        public TransactionQueryOptions Parameter(object paramValue)
        {
            _arguments.Add(paramValue);
            return this;
        }

        /// <summary>
        /// Sets multiple parameter values by position.
        /// </summary>
        /// <param name="values">The values to include.</param>
        /// <returns>The same TransactionsQueryOptions instance.</returns>
        public TransactionQueryOptions Parameter(params object[] values)
        {
            _arguments.AddRange(values);
            return this;
        }

        /// <summary>
        /// Sets the Scan Consistency value for this query.
        /// </summary>
        /// <param name="scanConsistency">The ScanConsistency value</param>
        /// <returns>The same TransactionsQueryOptions instance.</returns>
        public TransactionQueryOptions ScanConsistency(QueryScanConsistency scanConsistency)
        {
            _scanConsistency = scanConsistency;
            return this;
        }

        /// <summary>
        /// Sets a value indicating whether or not to use FlexIndex.
        /// </summary>
        /// <param name="flexIndex">The FlexIndex boolean.</param>
        /// <returns>The same TransactionsQueryOptions instance.</returns>
        public TransactionQueryOptions FlexIndex(bool flexIndex)
        {
            _flexIndex = flexIndex;
            return this;
        }

        /// <summary>
        /// Sets the user-defined TypeSerializer to use for parameter values and results.
        /// </summary>
        /// <param name="serializer">A Type Serializer, such as DefaultJsonSerializer or a custom serializer.</param>
        /// <returns>The same TransactionsQueryOptions instance.</returns>
        public TransactionQueryOptions Serializer(ITypeSerializer serializer)
        {
            _serializer = serializer;
            return this;
        }

        /// <summary>
        /// Supports providing a custom client context ID for this query.
        /// <p>
        /// If no client context ID is provided by the user, a UUID is generated and sent automatically so by default it is
        /// always possible to identify a query when debugging. If you do not want to send one, pass either null or an empty
        /// string to this method.
        /// </p>
        /// </summary>
        /// <param name="clientContextId">The client context ID - if null or empty it will not be sent.</param>
        /// <returns>The same TransactionsQueryOptions instance.</returns>
        public TransactionQueryOptions ClientContextId(string clientContextId)
        {
            _clientContextId = clientContextId;
            return this;
        }

        /// <summary>
        /// Allows customizing how long the query engine is willing to wait until the index catches up to whatever scan
        /// consistency is asked for in this query.
        /// </summary>
        /// <param name="scanWait">The maximum duration the query engine is willing to wait before failing.</param>
        /// <returns>The same TransactionsQueryOptions instance.</returns>
        public TransactionQueryOptions ScanWait(TimeSpan scanWait)
        {
            _scanWait = scanWait;
            return this;
        }

        /// <summary>
        /// Supports customizing the maximum buffered channel size between the indexer and the query service.
        /// </summary>
        /// <remarks>This is an advanced API and should only be tuned with care. Use 0 or a negative number to disable.</remarks>
        /// <param name="capacity">The scan cap size, use 0 or negative number to disable.</param>
        /// <returns>The same TransactionsQueryOptions instance.</returns>
        public TransactionQueryOptions ScanCap(int capacity)
        {
            _scanCap = capacity;
            return this;
        }

        /// <summary>
        /// Supports customizing the number of items execution operators can batch for fetch from the KV layer on the server.
        /// </summary>
        /// <remarks>This is an advanced API and should only be tuned with care.</remarks>
        /// <param name="batchSize">The pipeline batch size.</param>
        /// <returns>The same TransactionsQueryOptions instance</returns>
        public TransactionQueryOptions PipelineBatch(int batchSize)
        {
            _pipelineBatch = batchSize;
            return this;
        }

        /// <summary>
        /// Allows customizing the maximum number of items each execution operator can buffer between various operators on the server.
        /// </summary>
        /// <remarks>This is an advanced API and should only be tuned with care.</remarks>
        /// <param name="capacity">The pipeline cap size</param>
        /// <returns>The same TransactionsQueryOptions instance</returns>
        public TransactionQueryOptions PipelineCap(int capacity)
        {
            _pipelineCap = capacity;
            return this;
        }

        /// <summary>
        /// Allows explicitly marking a query as being readonly and not mutating and documents on the server side.
        /// <para>
        /// In addition to providing some security in that you are not accidentally modifying data, setting this flag to true
        /// also helps the client to more proactively retry and re-dispatch a query since then it can be sure it is idempotent.
        /// As a result, if your query is readonly then it is a good idea to set this flag.
        ///</para>
        /// <para>
        /// If set to true, then (at least) the following statements are not allowed:
        /// <ol>
        ///  <li>CREATE INDEX</li>
        ///  <li>DROP INDEX</li>
        ///  <li>INSERT</li>
        ///  <li>MERGE</li>
        ///  <li>UPDATE</li>
        ///  <li>UPSERT</li>
        ///  <li>DELETE</li>
        /// </ol>
        /// </para>
        /// </summary>
        /// <param name="readonly">True if readonly should be set, false is the default and will use the server side default.</param>
        /// <returns>The same TransactionsQueryOptions instance</returns>
        public TransactionQueryOptions Readonly(bool @readonly)
        {
            _readonly = @readonly;
            return this;
        }

        /// <summary>
        /// Allows turning this request into a prepared statement query.
        /// <para>
        /// If set to false, the SDK will transparently perform "prepare and execute" logic the first time this query
        /// is seen and then subsequently reuse the prepared statement name when sending it to the server. If a query is
        /// executed frequently, this is a good way to speed it up since it will save the server the task of re-parsing
        /// and analyzing the query.
        /// </para>
        /// <para>
        /// If you are using prepared statements, make sure that if certain parts of the query string change you are
        /// using named or positional parameters. If the statement string itself changes it cannot be cached.
        /// </para>
        /// </summary>
        /// <param name="adhoc">If set to false this query will be turned into a prepared statement query.</param>
        /// <returns>The same TransactionsQueryOptions instance</returns>
        public TransactionQueryOptions AdHoc(bool adhoc)
        {
            _adhoc = adhoc;
            return this;
        }

        /// <summary>Allows providing custom JSON key/value pairs for advanced usage.</summary>
        /// <returns>The same TransactionsQueryOptions instance</returns>
        public TransactionQueryOptions Raw(string key, object val)
        {
            switch (key)
            {
                case "txid":
                case "txdata":
                    throw new ArgumentOutOfRangeException(nameof(key), $"'{key}' is reserved for internal use.");
            }

            _rawParameters.Add(key, val);
            return this;
        }




        /// <returns>The same TransactionsQueryOptions instance</returns>
        public TransactionQueryOptions Timeout(TimeSpan timeout)
        {
            _timeout = timeout;
            return this;
        }
    }
}

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2024 Couchbase, Inc.
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







