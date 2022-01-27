using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.Core.Compatibility;
using Couchbase.Core.IO.Serializers;
using Couchbase.Query;

namespace Couchbase.Transactions.Config
{
    /// <summary>
    /// A limited subset of <see cref="QueryOptions"/> that are usable in Transactions.
    /// </summary>
    public class TransactionQueryOptions
    {
        // if you add anything to this section, add a corresponding 'if' block to the Build() method.
        private Dictionary<string, object> _parameters = new();
        private List<object> _arguments = new();
        private Dictionary<string, object> _rawParameters = new();
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
        /// <returns>A new instance of QueryOptions</returns>
        internal QueryOptions Build()
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

        public TransactionQueryOptions Parameter(string key, object val)
        {
            _parameters.Add(key, val);
            return this;
        }

        public TransactionQueryOptions Parameter(object paramValue)
        {
            _arguments.Add(paramValue);
            return this;
        }

        public TransactionQueryOptions Parameter(params object[] values)
        {
            _arguments.AddRange(values);
            return this;
        }

        public TransactionQueryOptions ScanConsistency(QueryScanConsistency scanConsistency)
        {
            _scanConsistency = scanConsistency;
            return this;
        }

        public TransactionQueryOptions FlexIndex(bool flexIndex)
        {
            _flexIndex = flexIndex;
            return this;
        }

        public TransactionQueryOptions Serializer(ITypeSerializer serializer)
        {
            _serializer = serializer;
            return this;
        }

        public TransactionQueryOptions ClientContextId(string clientContextId)
        {
            _clientContextId = clientContextId;
            return this;
        }

        public TransactionQueryOptions ScanWait(TimeSpan scanWait)
        {
            _scanWait = scanWait;
            return this;
        }

        public TransactionQueryOptions ScanCap(int capacity)
        {
            _scanCap = capacity;
            return this;
        }

        public TransactionQueryOptions PipelineBatch(int batchSize)
        {
            _pipelineBatch = batchSize;
            return this;
        }

        public TransactionQueryOptions PipelineCap(int capacity)
        {
            _pipelineCap = capacity;
            return this;
        }

        public TransactionQueryOptions Readonly(bool @readonly)
        {
            _readonly = @readonly;
            return this;
        }

        public TransactionQueryOptions AdHoc(bool adhoc)
        {
            _adhoc = adhoc;
            return this;
        }

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
 *    @copyright 2021 Couchbase, Inc.
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
