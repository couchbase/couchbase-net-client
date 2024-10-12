using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using Couchbase.Core;
using Couchbase.Core.Compatibility;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Serializers.SystemTextJson;
using Couchbase.Core.Retry;
using Couchbase.Core.Utils;
using Couchbase.Utils;
using ByteConverter = Couchbase.Core.IO.Converters.ByteConverter;

#nullable enable

namespace Couchbase.Query
{
    /// <summary>
    /// Options to control execution of a N1QL query.
    /// </summary>
    [DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
    public class QueryOptions
    {
        private const string GetFormValuesAsJsonUnreferencedCodeMessage =
            "GetFormValuesAsJson uses Newtonsoft.Json which requires unreferenced code and is incompatible with trimming.";
        private const string GetFormValuesAsJsonDynamicCodeMessage =
            "GetFormValuesAsJson uses Newtonsoft.Json which may required dynamic code.";

        internal static QueryOptions Default { get; } = new();
        public static readonly ReadOnlyRecord DefaultReadOnly = Default.AsReadOnly();

        private readonly List<object?> _arguments = new List<object?>();
        private readonly Dictionary<string, object> _parameters = new Dictionary<string, object>();
        private readonly Dictionary<string, object> _rawParameters = new Dictionary<string, object>();
        private bool _autoExecute;
        private bool _includeMetrics;
        private int? _maxServerParallelism;
        private int? _pipelineBatch;
        private int? _pipelineCapacity;
        private QueryPlan? _preparedPayload;
        private QueryProfile _profile = QueryProfile.Off;
        private bool? _readOnly;
        private int? _scanCapacity;
        private QueryScanConsistencyInternal? _scanConsistency;
        private Dictionary<string, Dictionary<string, ScanVectorComponent>>? _scanVectors;
        private TimeSpan? _scanWait;
        private string? _statement;
        private TimeSpan? _timeOut;
        private bool _flexIndex;
        private volatile bool _isUsed;
        private bool _preserveExpiry;
        private bool? _useReplica;

        internal QueryOptions CloneIfUsedAlready()
        {
            var cloneNow = _isUsed;
            _isUsed = true;

            if (cloneNow)
            {
                var queryOptions = new QueryOptions()
                    .Statement(_statement!)
                    .AdHoc(IsAdHoc)
                    .AutoExecute(_autoExecute)
                    .CancellationToken(Token)
                    .ClientContextId(CurrentContextId ?? Guid.NewGuid().ToString())
                    .FlexIndex(_flexIndex)
                    .PreserveExpiry(_preserveExpiry)
                    .Profile(_profile);

                queryOptions._scanVectors = _scanVectors;
                queryOptions.Metrics(_includeMetrics);

                if (_arguments is not null)
                {
                    foreach (var arg in _arguments)
                    {
                        queryOptions.Parameter(arg);
                    }
                }

                if (_rawParameters is not null)
                {
                    foreach (var arg in _rawParameters)
                    {
                        queryOptions.Raw(arg.Key, arg.Value);
                    }
                }

                if (_parameters is not null)
                {
                    foreach (var arg in _parameters)
                    {
                        queryOptions.Parameter(arg.Key, arg.Value);
                    }
                }

                if (_maxServerParallelism.HasValue)
                {
                    queryOptions.MaxServerParallelism(_maxServerParallelism.Value);
                }
                if (_pipelineCapacity.HasValue)
                {
                    queryOptions.PipelineCap(_pipelineCapacity.Value);
                }
                if (_pipelineBatch.HasValue)
                {
                    queryOptions.PipelineBatch(_pipelineBatch.Value);
                }
                if (_readOnly.HasValue)
                {
                    queryOptions.Readonly(_readOnly.Value);
                }
                if (_scanCapacity.HasValue)
                {
                    queryOptions.ScanCap(_scanCapacity.Value);
                }
                if (_timeOut.HasValue)
                {
                    queryOptions.Timeout(_timeOut.Value);
                }
                if(_scanWait.HasValue)
                {
                    queryOptions.ScanWait(_scanWait.Value);
                }
                if (_useReplica.HasValue)
                {
                    queryOptions.UseReplica(_useReplica);
                }

                queryOptions._scanConsistency = _scanConsistency;
                queryOptions.Serializer = Serializer;
                queryOptions.RequestSpanValue = RequestSpanValue;
                queryOptions.BucketName = BucketName;
                queryOptions.ScopeName = ScopeName;
                queryOptions.QueryContext = QueryContext;
                return queryOptions;
            }

            return this;
        }

        /// <summary>
        /// Creates a new QueryOptions object.
        /// </summary>
        public QueryOptions()
        {
        }

        /// <summary>
        /// Creates a new QueryOptions object with a N1QL query statement.
        /// </summary>
        /// <param name="statement">A N1QL query statement.</param>
        public QueryOptions(string statement) : this()
        {
            _statement = statement;
            _preparedPayload = null;
        }

        /// <summary>
        /// Creates a new QueryOptions object with an existing <see cref="QueryPlan"/>.
        /// </summary>
        /// <param name="plan">The <see cref="QueryPlan"/>.</param>
        /// <param name="originalStatement">The original N1QL query statement used to generate the plan.</param>
        public QueryOptions(QueryPlan plan, string originalStatement) : this()
        {
            _statement = originalStatement;
            _preparedPayload = plan;
        }

        /// <summary>
        /// Allows querying data from replicas.
        /// True: Allows the server to read potentially stale data from replica vBuckets.
        /// False: The server must use up-to-date data from primaries (nodes which own the vBucket containing the data).
        /// </summary>
        /// <param name="useReplica"></param>
        /// <returns></returns>
        public QueryOptions UseReplica(bool? useReplica)
        {
            _useReplica = useReplica;
            return this;
        }

        /// <summary>
        /// For internal use to get the value of UseReplica.
        /// </summary>
        internal bool UseReplicaHasValue => _useReplica.HasValue;

        /// <summary>
        /// The bucket name for tracing.
        /// </summary>
        /// <remarks>For internal use only</remarks>
        internal string? BucketName { get; set; }

        /// <summary>
        /// The bucket name for tracing.
        /// </summary>
        /// <remarks>For internal use only</remarks>
        internal string? ScopeName { get; set; }

        internal IRequestSpan? RequestSpanValue { get; private set; }

        public QueryOptions RequestSpan(IRequestSpan span)
        {
            RequestSpanValue = span;
            return this;
        }

        internal IRetryStrategy? RetryStrategyValue { get; set; }

        /// <summary>
        /// Overrides the global <see cref="IRetryStrategy"/> defined in <see cref="ClusterOptions"/> for a request.
        /// </summary>
        /// <param name="retryStrategy">The <see cref="IRetryStrategy"/> to use for a single request.</param>
        /// <returns>The options.</returns>
        public QueryOptions RetryStrategy(IRetryStrategy retryStrategy)
        {
            RetryStrategyValue = retryStrategy;
            return this;
        }

        internal CancellationToken Token { get; set; } = System.Threading.CancellationToken.None;

        internal TimeSpan? TimeoutValue
        {
            get => _timeOut;
            set => _timeOut = value;
        }

        internal string? StatementValue => _statement;

        [InterfaceStability(Level.Volatile)]
        public Uri? LastDispatchedNode { get; set; }

        internal string GetAllParametersAsJson(ITypeSerializer serializer)
        {
            using var stream = new MemoryStream();

            using (var writer = new Utf8JsonWriter(stream))
            {
                if (serializer is SystemTextJsonSerializer stjSerializer)
                {
                    WriteAllParametersAsSystemTextJson(writer, stjSerializer);
                }
                else
                {
                    WriteAllParametersAsGenericJson(writer, serializer);
                }
            }

            if (stream.TryGetBuffer(out ArraySegment<byte> arraySegment))
            {
                return ByteConverter.ToString(arraySegment.AsSpan());
            }

            // Fallback to extracting a new array from the MemoryStream
            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }

        private void WriteAllParametersAsSystemTextJson(Utf8JsonWriter writer, SystemTextJsonSerializer serializer)
        {
            writer.WriteStartObject();

            writer.WriteStartObject("Named");
            foreach (var parameter in _parameters)
            {
                writer.WritePropertyName(parameter.Key);
                serializer.Serialize(writer, parameter.Value);
            }
            writer.WriteEndObject();

            writer.WriteStartObject("Raw");
            foreach (var parameter in _rawParameters)
            {
                writer.WritePropertyName(parameter.Key);
                serializer.Serialize(writer, parameter.Value);
            }
            writer.WriteEndObject();

            writer.WriteStartArray("Positional");
            foreach (var parameter in _arguments)
            {
                serializer.Serialize(writer, parameter);
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        private void WriteAllParametersAsGenericJson(Utf8JsonWriter writer, ITypeSerializer serializer)
        {
            writer.WriteStartObject();

            writer.WriteStartObject("Named");
            foreach (var parameter in _parameters)
            {
                writer.WritePropertyName(parameter.Key);
                writer.WriteRawValue(serializer.Serialize(parameter.Value));
            }
            writer.WriteEndObject();

            writer.WriteStartObject("Raw");
            foreach (var parameter in _rawParameters)
            {
                writer.WritePropertyName(parameter.Key);
                writer.WriteRawValue(serializer.Serialize(parameter.Value));
            }
            writer.WriteEndObject();

            writer.WriteStartArray("Positional");
            foreach (var parameter in _arguments)
            {
                writer.WriteRawValue(serializer.Serialize(parameter));
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        /// <summary>
        ///     Returns true if the request is a prepared statement
        /// </summary>
        [MemberNotNullWhen(true, nameof(_preparedPayload))]
        public bool IsPrepared => _preparedPayload != null;

        /// <summary>
        ///     Gets a value indicating whether this query statement is to executed in an ad-hoc manner.
        /// </summary>
        /// <value>
        ///     <c>true</c> if this instance is ad-hoc; otherwise, <c>false</c>.
        /// </value>
        public bool IsAdHoc { get; private set; } = true;

        /// <summary>
        ///     Gets the context identifier for the N1QL query request/response. Useful for debugging.
        /// </summary>
        /// <remarks>This value changes for every request./></remarks>
        /// <value>
        ///     The context identifier.
        /// </value>
        public string? CurrentContextId { get; private set; }

        /// <summary>
        ///     Custom <see cref="ITypeSerializer" /> to use when deserializing query results.
        /// </summary>
        /// <remarks>Null will use the default <see cref="ITypeSerializer" />.</remarks>
        public ITypeSerializer? Serializer { get; set; }

        internal bool IsReadOnly => _readOnly.HasValue && _readOnly.Value;

        /// <summary>
        ///     Provides a means of ensuring "read your own writes" or RYOW consistency on the current query.
        /// </summary>
        /// <remarks>Note: <see cref="ScanConsistency" /> will be overwritten to <see cref="QueryScanConsistencyInternal.AtPlus" />.</remarks>
        /// <param name="mutationState">State of the mutation.</param>
        /// <returns>A reference to the current <see cref="QueryOptions" /> for method chaining.</returns>
        public QueryOptions ConsistentWith(MutationState mutationState)
        {
#pragma warning disable 618
            ScanConsistencyInternal(QueryScanConsistencyInternal.AtPlus);
#pragma warning restore 618
            _scanVectors = new Dictionary<string, Dictionary<string, ScanVectorComponent>>();
            foreach (var token in mutationState)
                if (_scanVectors.TryGetValue(token.BucketRef, out var vector))
                {
                    var bucketId = token.VBucketId.ToStringInvariant();
                    if (vector.TryGetValue(bucketId, out var bucketRef))
                    {
                        if (bucketRef.SequenceNumber < token.SequenceNumber)
                            vector[bucketId] = new ScanVectorComponent
                            {
                                SequenceNumber = token.SequenceNumber,
                                VBucketUuid = token.VBucketUuid
                            };
                    }
                    else
                    {
                        vector.Add(token.VBucketId.ToStringInvariant(),
                            new ScanVectorComponent
                            {
                                SequenceNumber = token.SequenceNumber,
                                VBucketUuid = token.VBucketUuid
                            });
                    }
                }
                else
                {
                    _scanVectors.Add(token.BucketRef, new Dictionary<string, ScanVectorComponent>
                    {
                        {
                            token.VBucketId.ToStringInvariant(),
                            new ScanVectorComponent
                            {
                                SequenceNumber = token.SequenceNumber,
                                VBucketUuid = token.VBucketUuid
                            }
                        }
                    });
                }

            return this;
        }

        /// <summary>
        ///     Specifies the maximum parallelism for the query. A zero or negative value means the number of logical
        ///     cpus will be used as the parallelism for the query. There is also a server wide max_parallelism parameter
        ///     which defaults to 1. If a request includes max_parallelism, it will be capped by the server max_parallelism.
        ///     If a request does not include max_parallelism, the server wide max_parallelism will be used.
        /// </summary>
        /// <param name="parallelism"></param>
        /// <returns></returns>
        /// <value>
        ///     The maximum server parallelism.
        /// </value>
        public QueryOptions MaxServerParallelism(int parallelism)
        {
            _maxServerParallelism = parallelism;
            return this;
        }

        /// <summary>
        ///     If set to false, the client will try to perform optimizations
        ///     transparently based on the server capabilities, like preparing the statement and
        ///     then executing a query plan instead of the raw query.
        /// </summary>
        /// <param name="adHoc">if set to <c>false</c> the query will be optimized if possible.</param>
        /// <returns></returns>
        /// <remarks>
        ///     The default is <c>true</c>; the query will executed in an ad-hoc manner,
        ///     without special optimizations.
        /// </remarks>
        public QueryOptions AdHoc(bool adHoc)
        {
            IsAdHoc = adHoc;
            return this;
        }

        /// <summary>
        ///     Sets a N1QL statement to be executed in an optimized way using the given queryPlan.
        /// </summary>
        /// <param name="preparedPlan">The <see cref="Query.QueryPlan" /> that was prepared beforehand.</param>
        /// <param name="originalStatement">The original statement (eg. SELECT * FROM default) that the user attempted to optimize</param>
        /// <returns>A reference to the current <see cref="QueryOptions" /> for method chaining.</returns>
        /// <remarks>Required if statement not provided, will erase a previously set Statement.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="preparedPlan" /> is <see langword="null" />.</exception>
        public QueryOptions Prepared(QueryPlan preparedPlan, string originalStatement)
        {
            if (string.IsNullOrWhiteSpace(originalStatement))
                throw new ArgumentNullException(nameof(originalStatement));

            _statement = originalStatement;
            _preparedPayload = preparedPlan ?? throw new ArgumentNullException(nameof(preparedPlan));
            return this;
        }

        /// <summary>
        ///     Sets a N1QL statement to be executed.
        /// </summary>
        /// <param name="statement">
        ///     Any valid N1QL statement for a POST request, or a read-only N1QL statement (SELECT, EXPLAIN)
        ///     for a GET request.
        /// </param>
        /// <returns>
        ///     A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">statement</exception>
        /// <remarks>
        ///     Will erase a previous optimization of a statement using Prepared.
        /// </remarks>
        internal QueryOptions Statement(string statement)
        {
            if (string.IsNullOrWhiteSpace(statement)) throw new ArgumentNullException(nameof(statement));
            _statement = statement;
            _preparedPayload = null;
            return this;
        }

        /// <summary>
        ///     Sets the maximum time to spend on the request.
        /// </summary>
        /// <param name="timeOut">Maximum time to spend on the request</param>
        /// <returns>
        ///     A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        /// <remarks>
        ///     Optional - the default is 0ms, which means the request runs for as long as it takes.
        /// </remarks>
        public QueryOptions Timeout(TimeSpan timeOut)
        {
            _timeOut = timeOut;
            return this;
        }

        /// <summary>
        ///     If a GET request, this will always be true otherwise false.
        /// </summary>
        /// <param name="readOnly">True for get requests.</param>
        /// <returns>
        ///     A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        /// <remarks>
        ///     Any value set here will be overridden by the type of request sent.
        /// </remarks>
        [Obsolete("Use QueryOptions.Readonly property instead.")]
        public QueryOptions ReadOnly(bool readOnly)
        {
            _readOnly = readOnly;
            return this;
        }

        /// <summary>
        ///     If a GET request, this will always be true otherwise false.
        /// </summary>
        /// <param name="readOnly">True for get requests.</param>
        /// <returns>
        ///     A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        /// <remarks>
        ///     Any value set here will be overridden by the type of request sent.
        /// </remarks>
        public QueryOptions Readonly(bool readOnly)
        {
            _readOnly = readOnly;
            return this;
        }

        /// <summary>
        ///     Specifies that metrics should be returned with query results.
        /// </summary>
        /// <param name="includeMetrics">True to return query metrics.</param>
        /// <returns>
        ///     A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        /// <remarks>
        ///     Optional.
        /// </remarks>
            public QueryOptions Metrics(bool includeMetrics)
        {
            _includeMetrics = includeMetrics;
            return this;
        }

        /// <summary>
        ///     Adds a named parameter to the parameters to the statement or prepared statement.
        /// </summary>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="value">The value of the parameter.</param>
        /// <returns>
        ///     A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        /// <remarks>
        ///     Optional.
        /// </remarks>
        public QueryOptions Parameter(string name, object value)
        {
            _parameters.Add(name, value);
            return this;
        }

        /// <summary>
        ///     Adds a positional parameter to the parameters to the statement or prepared statement.
        /// </summary>
        /// <param name="value">The value of the positional parameter.</param>
        /// <returns>
        ///     A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        /// <remarks>
        ///     Optional.
        /// </remarks>
        public QueryOptions Parameter(object? value)
        {
            _arguments.Add(value);
            return this;
        }

        /// <summary>
        ///     Adds a collection of named parameters to the parameters to the statement or prepared statement.
        /// </summary>
        /// <param name="parameters">A list of <see cref="KeyValuePair{K, V}" /> to be sent.</param>
        /// <returns>
        ///     A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        /// <remarks>
        ///     Optional.
        /// </remarks>
        public QueryOptions Parameter(params KeyValuePair<string, object>[] parameters)
        {
            if (_arguments?.Any() ?? false)
            {
                throw new ArgumentException("Cannot combine positional and named query parameters.");
            }

            foreach (var parameter in parameters) _parameters.Add(parameter.Key, parameter.Value);

            return this;
        }

        /// <summary>
        ///     Adds a list of positional parameters to the statement or prepared statement.
        /// </summary>
        /// <param name="parameters">A list of positional parameters.</param>
        /// <returns></returns>
        public QueryOptions Parameter(params object[] parameters)
        {
            if (_parameters?.Any() ?? false)
            {
                throw new ArgumentException("Cannot combine positional and named query parameters.");
            }

            foreach (var parameter in parameters) _arguments.Add(parameter);

            return this;
        }

        /// <summary>
        /// (v.7.1.0 and onwards)
        /// Tells the query engine to preserve expiration values set on any documents modified by this query.
        /// </summary>
        /// <param name="preserveExpiry">If expiration values should be preserved, the default is false.</param>
        /// <returns>A reference to the current <see cref="QueryOptions" /> for method chaining.</returns>
        [InterfaceStability(Level.Uncommitted)]
        public QueryOptions PreserveExpiry(bool preserveExpiry)
        {
            _preserveExpiry = preserveExpiry;
            return this;
        }

        /// <summary>
        ///     Specifies the consistency guarantee/constraint for index scanning.
        /// </summary>
        /// <param name="scanConsistency">Specify the consistency guarantee/constraint for index scanning.</param>
        /// <returns>
        ///     A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        /// <remarks>
        ///     Optional.
        /// </remarks>
        public QueryOptions ScanConsistency(QueryScanConsistency scanConsistency) =>
            ScanConsistencyInternal((QueryScanConsistencyInternal) scanConsistency);

        /// <summary>
        ///     Specifies the consistency guarantee/constraint for index scanning.
        /// </summary>
        /// <param name="scanConsistency">Specify the consistency guarantee/constraint for index scanning.</param>
        /// <returns>
        ///     A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        /// <exception cref="InvalidEnumArgumentException">Invalid <paramref name="scanConsistency"/>.</exception>
        /// <remarks>
        ///     Used internally to allow <see cref="ConsistentWith"/> to set the consistency to <see cref="QueryScanConsistencyInternal.AtPlus"/>.
        /// </remarks>
        internal QueryOptions ScanConsistencyInternal(QueryScanConsistencyInternal scanConsistency)
        {
            if (scanConsistency < QueryScanConsistencyInternal.NotBounded ||
                scanConsistency > QueryScanConsistencyInternal.AtPlus)
            {
                throw new InvalidEnumArgumentException(nameof(scanConsistency), (int) scanConsistency, typeof(QueryScanConsistencyInternal));
            }

            _scanConsistency = scanConsistency;
            return this;
        }

        /// <summary>
        ///     Specifies the maximum time the client is willing to wait for an index to catch up to the vector timestamp in the
        ///     request. If an index has to catch up, and the <see cref="ScanWait" /> time is exceed doing so, an error is
        ///     returned.
        /// </summary>
        /// <param name="scanWait">The maximum time the client is willing to wait for index to catch up to the vector timestamp.</param>
        /// <returns>
        ///     A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        /// <remarks>
        ///     Optional.
        /// </remarks>
        public QueryOptions ScanWait(TimeSpan scanWait)
        {
            _scanWait = scanWait;
            return this;
        }

        /// <summary>
        ///     Client Context ID.
        ///     If no client context ID is provided on this option, a UUID is generated and sent
        ///     automatically so by default it is always possible to identify a query when debugging.
        /// </summary>
        /// <param name="clientContextId">The client context identifier.</param>
        /// <returns>A reference to the current <see cref="QueryOptions" /> for method chaining.</returns>
        public QueryOptions ClientContextId(string clientContextId)
        {
            //this is seeded in the ctor
            if (clientContextId != null) CurrentContextId = clientContextId;

            return this;
        }

        /// <summary>
        ///     Adds a raw query parameter and value to the query.
        ///     NOTE: This is uncommitted and may change in the future.
        /// </summary>
        /// <param name="name">The paramter name.</param>
        /// <param name="value">The parameter value.</param>
        /// <returns>A reference to the current <see cref="QueryOptions" /> for method chaining.</returns>
        public QueryOptions Raw(string name, object value)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Parameter name cannot be null or empty.");

            _rawParameters.Add(name, value);
            return this;
        }

        /// <summary>
        ///     Sets maximum buffered channel size between the indexer client
        ///     and the query service for index scans.
        ///     This parameter controls when to use scan backfill.
        ///     Use 0 or a negative number to disable.
        /// </summary>
        /// <param name="capacity">The maximum number of channels.</param>
        /// <returns>
        ///     A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        public QueryOptions ScanCap(int capacity)
        {
            _scanCapacity = capacity;
            return this;
        }

        /// <summary>
        ///     Sets the number of items execution operators can batch for
        ///     fetch from the KV.
        /// </summary>
        /// <param name="batchSize">The maximum number of items.</param>
        /// <returns>
        ///     A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        public QueryOptions PipelineBatch(int batchSize)
        {
            _pipelineBatch = batchSize;
            return this;
        }

        /// <summary>
        ///     Sets maximum number of items each execution operator can buffer
        ///     between various operators.
        /// </summary>
        /// <param name="capacity">The maximum number of items.</param>
        /// <returns>
        ///     A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        public QueryOptions PipelineCap(int capacity)
        {
            _pipelineCapacity = capacity;
            return this;
        }

        /// <summary>
        ///     Set the <see cref="QueryProfile"/> information to be returned along with the query results.
        /// </summary>
        /// <param name="profile">The <see cref="QueryProfile"/>.</param>
        /// <returns>
        ///     A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        public QueryOptions Profile(QueryProfile profile)
        {
            _profile = profile;
            return this;
        }

        /// <summary>
        ///     Set the <see cref="CancellationToken"/> which will cancel the query if it is incomplete.
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>
        ///     A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        public QueryOptions CancellationToken(CancellationToken cancellationToken)
        {
            Token = cancellationToken;
            return this;
        }

        /// <summary>
        /// Tells the query engine to use a flex index (utilizing the search service).
        /// </summary>
        /// <param name="flexIndex">true if a flex index should be used, false is the default</param>
        /// <returns>A reference to the current <see cref="QueryOptions" /> for method chaining.</returns>
        /// <remarks>This feature is Uncommitted and may change in the future.</remarks>
        public QueryOptions FlexIndex(bool flexIndex)
        {
            _flexIndex = flexIndex;
            return this;
        }

        /// <summary>
        ///The alias for the namespace:bucket:scope:collection
        /// </summary>
        /// <returns></returns>
        internal string? QueryContext { get; set; }

        internal QueryOptions QueryPlan(QueryPlan queryPlan)
        {
            _preparedPayload = queryPlan;
            return this;
        }

        internal QueryOptions AutoExecute(bool autoExecute)
        {
            _autoExecute = autoExecute;
            return this;
        }

        internal QueryOptionsDto CreateDto(ITypeSerializer serializer)
        {
            if (string.IsNullOrWhiteSpace(_statement) && _preparedPayload == null)
            {
                ThrowHelper.ThrowInvalidOperationException("A statement or prepared plan must be provided.");
            }
            if (_scanVectors is not null && _scanConsistency.GetValueOrDefault() != QueryScanConsistencyInternal.AtPlus)
            {
                ThrowHelper.ThrowInvalidOperationException(
                    "Only ScanConsistency.AtPlus is supported for this query request.");
            }

            var dto = new QueryOptionsDto
            {
                AutoExecute = _autoExecute,
                ClientContextId = CurrentContextId,
                FlexIndex = _flexIndex,
                IncludeMetrics = _includeMetrics,
                MaxServerParallelism = _maxServerParallelism?.ToString(),
                PipelineBatch = _pipelineBatch,
                PipelineCapacity = _pipelineCapacity,
                PreserveExpiry = _preserveExpiry,
                Profile = _profile,
                QueryContext = QueryContext,
                ReadOnly = _readOnly,
                ScanCapacity = _scanCapacity,
                ScanConsistency = _scanConsistency,
                ScanVectors = _scanVectors,
                ScanWait = _scanWait,
                Timeout = _timeOut
            };

            if (_useReplica.HasValue)
            {
                dto.UseReplica = _useReplica.Value ? "on" : "off";
            }

            if (IsPrepared)
            {
                dto.Prepared = _preparedPayload.Name;

                // don't include empty plan
                if (!string.IsNullOrEmpty(_preparedPayload.EncodedPlan))
                {
                    dto.PreparedEncoded = _preparedPayload.EncodedPlan;
                }
            }
            else
            {
                dto.Statement = _statement;
            }

            if (_arguments is {Count: > 0})
            {
                dto.Arguments = _arguments.Select(p => new TypeSerializerWrapper(serializer, p)).ToList();
            }

            if (_parameters is {Count: > 0})
            {
                dto.AdditionalProperties ??= new Dictionary<string, object>();

                foreach (var parameter in _parameters)
                {
                    dto.AdditionalProperties.Add(
                        parameter.Key.Contains("$") ? parameter.Key : "$" + parameter.Key,
                        new TypeSerializerWrapper(serializer, parameter.Value));
                }
            }

            if (_rawParameters is {Count: > 0})
            {
                dto.AdditionalProperties ??= new Dictionary<string, object>();

                foreach (var parameter in _rawParameters)
                {
                    dto.AdditionalProperties.Add(parameter.Key, new TypeSerializerWrapper(serializer, parameter.Value));
                }
            }

            return dto;
        }

        /// <summary>
        ///     Gets a <see cref="IDictionary{K, V}" /> of the name/value pairs to be POSTed to the service.
        /// </summary>
        /// <returns>
        ///     The <see cref="IDictionary{K, V}" /> of the name/value pairs to be POSTed to the service.
        /// </returns>
        /// <exception cref="System.ArgumentException">A statement or prepared plan must be provided.</exception>
        /// <remarks>
        ///     Since values will be POSTed as JSON, here we deal with unencoded typed values
        ///     (like ints, Lists, etc...) rather than only strings.
        /// </remarks>
        [RequiresUnreferencedCode(GetFormValuesAsJsonUnreferencedCodeMessage)]
        [RequiresDynamicCode(GetFormValuesAsJsonDynamicCodeMessage)]
        public IDictionary<string, object?> GetFormValues() => CreateDto(Serializer ?? DefaultSerializer.Instance).ToDictionary();

        /// <summary>
        /// Gets the JSON representation of this query for execution in a POST.
        /// </summary>
        /// <returns>The form values as a JSON object.</returns>
        [RequiresUnreferencedCode(GetFormValuesAsJsonUnreferencedCodeMessage)]
        [RequiresDynamicCode(GetFormValuesAsJsonDynamicCodeMessage)]
        public string GetFormValuesAsJson()
        {
            var formValues = CreateDto(Serializer ?? DefaultSerializer.Instance);

            return InternalSerializationContext.SerializeWithFallback(formValues, QuerySerializerContext.Default.QueryOptionsDto);
        }

        /// <summary>
        /// Gets the JSON representation of this query for execution in an HTTP POST.
        /// </summary>
        /// <returns>The <see cref="HttpContent"/>.</returns>
        internal HttpContent GetRequestBody(ITypeSerializer serializer, IFallbackTypeSerializerProvider fallbackTypeSerializerProvider)
        {
            var formValues = CreateDto(serializer);

            var stream = new MemoryStream(1024);
            try
            {
                InternalSerializationContext.SerializeWithFallback(stream, formValues, QuerySerializerContext.Default.QueryOptionsDto,
                    fallbackTypeSerializerProvider);
                stream.Position = 0;

                return new StreamContent(stream)
                {
                    Headers =
                    {
                        ContentType = new MediaTypeHeaderValue(MediaType.Json)
                        {
                            CharSet = "utf-8"
                        }
                    }
                };
            }
            catch
            {
                // Cleanup on exception
                stream.Dispose();
                throw;
            }
        }

        /// <summary>
        ///     Creates a new <see cref="QueryOptions" /> object.
        /// </summary>
        /// <returns></returns>
        public static QueryOptions Create()
        {
            return new QueryOptions();
        }

        /// <summary>
        ///     Creates a new <see cref="QueryOptions" /> object with the specified statement.
        /// </summary>
        /// <param name="statement">The statement.</param>
        /// <returns></returns>
        public static QueryOptions Create(string statement)
        {
            return new QueryOptions(statement);
        }

        /// <summary>
        ///     Creates a query using the given plan as an optimization for the originalStatement.
        /// </summary>
        /// <param name="plan">The plan.</param>
        /// <param name="originalStatement">The original statement, unoptimized.</param>
        /// <returns></returns>
        public static QueryOptions Create(QueryPlan plan, string originalStatement)
        {
            return new QueryOptions(plan, originalStatement);
        }

        private string DebuggerDisplay
        {
            [RequiresUnreferencedCode(GetFormValuesAsJsonUnreferencedCodeMessage)]
            [RequiresDynamicCode(GetFormValuesAsJsonDynamicCodeMessage)]
            get
            {
                try
                {
                    return "[" + GetFormValuesAsJson() + "]";
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        public void Deconstruct(out IReadOnlyList<object?> arguments,
            out IReadOnlyDictionary<string, object> parameters,
            out IReadOnlyDictionary<string, object> rawParameters,
            out bool autoExecute,
            out bool? includeMetrics,
            out int? maxServerParallelism,
            out int? pipelineBatch,
            out int? pipelineCapacity,
            out QueryPlan? preparedPayload,
            out QueryProfile profile,
            out bool? readOnly,
            out int? scanCapacity,
            out QueryScanConsistency scanConsistency,
            // out IReadOnlyDictionary<string, Dictionary<string, ScanVectorComponent>>? scanVectors,
            out TimeSpan? scanWait,
            out string? statement,
            out TimeSpan? timeOut,
            out bool flexIndex,
            out bool isUsed,
            out bool preserveExpiry,
            out string? bucketName,
            out string? scopeName,
            out IRequestSpan? requestSpanValue,
            out IRetryStrategy? retryStrategyValue,
            out CancellationToken token,
            out Uri? lastDispatchedNode,
            out bool isPrepared,
            out bool isAdHoc,
            out string? currentContextId,
            out ITypeSerializer? serializer,
            out string? queryContext,
            out bool? useReplica)
        {
            arguments = _arguments;
            parameters = _parameters;
            rawParameters = _rawParameters;
            autoExecute = _autoExecute;
            includeMetrics = _includeMetrics;
            maxServerParallelism = _maxServerParallelism;
            pipelineBatch = _pipelineBatch;
            pipelineCapacity = _pipelineCapacity;
            preparedPayload = _preparedPayload;
            profile = _profile;
            readOnly = _readOnly;
            scanCapacity = _scanCapacity;
            scanConsistency = ConvertScanConsistency(_scanConsistency);
            // scanVectors = _scanVectors;
            scanWait = _scanWait;
            statement = _statement;
            timeOut = _timeOut;
            flexIndex = _flexIndex;
            isUsed = _isUsed;
            preserveExpiry = _preserveExpiry;
            bucketName = BucketName;
            scopeName = ScopeName;
            requestSpanValue = RequestSpanValue;
            retryStrategyValue = RetryStrategyValue;
            token = Token;
            lastDispatchedNode = LastDispatchedNode;
            isPrepared = IsPrepared;
            isAdHoc = IsAdHoc;
            currentContextId = CurrentContextId;
            serializer = Serializer;
            queryContext = QueryContext;
            useReplica = _useReplica;
        }

        public ReadOnlyRecord AsReadOnly()
        {
            this.Deconstruct(
                out IReadOnlyList<object?> arguments,
                out IReadOnlyDictionary<string, object> parameters,
                out IReadOnlyDictionary<string, object> rawParameters,
                out bool autoExecute,
                out bool? includeMetrics,
                out int? maxServerParallelism,
                out int? pipelineBatch,
                out int? pipelineCapacity,
                out QueryPlan? preparedPayload,
                out QueryProfile profile,
                out bool? readOnly,
                out int? scanCapacity,
                out QueryScanConsistency scanConsistency,
                // out IReadOnlyDictionary<string, Dictionary<string, ScanVectorComponent>>? scanVectors,
                out TimeSpan? scanWait,
                out string? statement,
                out TimeSpan? timeOut,
                out bool flexIndex,
                out bool isUsed,
                out bool preserveExpiry,
                out string? bucketName,
                out string? scopeName,
                out IRequestSpan? requestSpanValue,
                out IRetryStrategy? retryStrategyValue,
                out CancellationToken token,
                out Uri? lastDispatchedNode,
                out bool isPrepared,
                out bool isAdHoc,
                out string? currentContextId,
                out ITypeSerializer? serializer,
                out string? queryContext,
                out bool? useReplica);

            return new ReadOnlyRecord(
                arguments,
                parameters,
                rawParameters,
                autoExecute,
                includeMetrics,
                maxServerParallelism,
                pipelineBatch,
                pipelineCapacity,
                preparedPayload,
                profile,
                readOnly,
                scanCapacity,
                scanConsistency,
                // scanVectors,
                scanWait,
                statement,
                timeOut,
                flexIndex,
                isUsed,
                preserveExpiry,
                bucketName,
                scopeName,
                requestSpanValue,
                retryStrategyValue,
                token,
                lastDispatchedNode,
                isPrepared,
                isAdHoc,
                currentContextId,
                serializer,
                queryContext,
                useReplica);
        }

        public record ReadOnlyRecord(
            IReadOnlyList<object?> Arguments,
            IReadOnlyDictionary<string, object> Parameters,
            IReadOnlyDictionary<string, object> RawParameters,
            bool AutoExecute,
            bool? IncludeMetrics,
            int? MaxServerParallelism,
            int? PipelineBatch,
            int? PipelineCapacity,
            QueryPlan? PreparedPayload,
            QueryProfile Profile,
            bool? ReadOnly,
            int? ScanCapacity,
            QueryScanConsistency ScanConsistency,
            // IReadOnlyDictionary<string, Dictionary<string, ScanVectorComponent>>? ScanVectors,
            TimeSpan? ScanWait,
            string? Statement,
            TimeSpan? TimeOut,
            bool FlexIndex,
            bool IsUsed,
            bool PreserveExpiry,
            string? BucketName,
            string? ScopeName,
            IRequestSpan? RequestSpan,
            IRetryStrategy? RetryStrategy,
            CancellationToken Token,
            Uri? LastDispatchedNode,
            bool IsPrepared,
            bool IsAdHoc,
            string? CurrentContextId,
            ITypeSerializer? Serializer,
            string? QueryContext,
            bool? UseReplica);

        private static QueryScanConsistency ConvertScanConsistency(QueryScanConsistencyInternal? scanConsistency)
        {
            if (scanConsistency is QueryScanConsistencyInternal.AtPlus or QueryScanConsistencyInternal.RequestPlus)
            {
                return QueryScanConsistency.RequestPlus;
            }

            return QueryScanConsistency.NotBounded;
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
