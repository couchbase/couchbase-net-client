using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Couchbase.Core;
using Couchbase.Core.DataMapping;
using Couchbase.Core.IO.Operations.Legacy;
using Couchbase.Core.IO.Operations.Legacy.Errors;
using Couchbase.Utils;
using Newtonsoft.Json;
using Encoding = Couchbase.Services.Query.Couchbase.N1QL.Encoding;

namespace Couchbase.Services.Query
{
    public class QueryOptions : IQueryOptions
    {
        private string _statement;
        private QueryPlan _preparedPayload;
        private TimeSpan? _timeOut = TimeSpan.Zero;
        private bool? _readOnly;
        private bool? _includeMetrics;
        private readonly Dictionary<string, object> _parameters = new Dictionary<string, object>();
        private readonly List<object> _arguments = new List<object>();
        private Format? _format;
        private Encoding? _encoding;
        private Compression? _compression;
        private ScanConsistency? _scanConsistency;
        private bool? _includeSignature;
        private TimeSpan? _scanWait;
        private bool? _pretty;
        private readonly Dictionary<string, string> _credentials = new Dictionary<string, string>();
        private string _clientContextId;
        private Uri _baseUri;
        private bool _prepareEncoded;
        private bool _adHoc = true;
        private int? _maxServerParallelism;
        private volatile uint _requestContextId;
        private Dictionary<string, Dictionary<string, List<object>>> _scanVectors;
        private bool? _useStreaming;
        private int? _scanCapacity;
        private int? _pipelineBatch;
        private int? _pipelineCapacity;
        private readonly Dictionary<string, object> _rawParameters = new Dictionary<string, object>();
        private QueryProfile _profile = QueryProfile.Off;

        public const string ForwardSlash = "/";
        public const string QueryOperator = "?";
        private const string QueryArgPattern = "{0}={1}&";
        public const string TimeoutArgPattern = "{0}={1}ms&";
        public const uint TimeoutDefault = 75000;

        public QueryOptions()
        {
            _clientContextId = QuerySequenceGenerator.GetNextAsString();
        }

        public QueryOptions(string statement):this()
        {
            _statement = statement;
            _preparedPayload = null;
            _prepareEncoded = false;
        }

        public QueryOptions(QueryPlan plan, string originalStatement):this()
        {
            _statement = originalStatement;
            _preparedPayload = plan;
            _prepareEncoded = true;
        }

        private struct QueryParameters
        {
            public const string Statement = "statement";
            public const string PreparedEncoded = "encoded_plan";
            public const string Prepared = "prepared";
            public const string Timeout = "timeout";
            public const string Readonly = "readonly";
            public const string Metrics = "metrics";
            public const string Args = "args";
            // ReSharper disable once UnusedMember.Local
            public const string BatchArgs = "batch_args";
            // ReSharper disable once UnusedMember.Local
            public const string BatchNamedArgs = "batch_named_args";
            public const string Format = "format";
            public const string Encoding = "encoding";
            public const string Compression = "compression";
            public const string Signature = "signature";
            public const string ScanConsistency = "scan_consistency";
            public const string ScanVectors = "scan_vectors";
            public const string ScanWait = "scan_wait";
            public const string Pretty = "pretty";
            public const string Creds = "creds";
            public const string ClientContextId = "client_context_id";
            public const string MaxServerParallelism = "max_parallelism";
            public const string ScanCapacity = "scan_cap";
            public const string PipelineBatch = "pipeline_batch";
            public const string PipelineCapacity = "pipeline_cap";
            public const string Profile = "profile";
        }

        /// <summary>
        /// Returns true if the request is a prepared statement
        /// </summary>
        public bool IsPrepared => _prepareEncoded;

        /// <summary>
        /// Gets a value indicating whether this query statement is to executed in an ad-hoc manner.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is ad-hoc; otherwise, <c>false</c>.
        /// </value>
        public bool IsAdHoc => _adHoc;

        /// <summary>
        /// Gets the context identifier for the N1QL query request/response. Useful for debugging.
        /// </summary>
        /// <remarks>This value changes for every request./></remarks>
        /// <value>
        /// The context identifier.
        /// </value>
        public string CurrentContextId => string.Format("{0}::{1}", _clientContextId, _requestContextId);

        /// <summary>
        /// Gets a value indicating whether this instance has been retried (if it's been optimized
        /// and prepared then the server marked it as stale/not runnable).
        /// </summary>
        /// <value><c>true</c> if this instance has been retried once, otherwise <c>false</c>.</value>
        public bool HasBeenRetried { get; set; }

        /// <summary>
        /// Custom <see cref="IDataMapper"/> to use when deserializing query results.
        /// </summary>
        /// <remarks>Null will use the default <see cref="IDataMapper"/>.</remarks>
        public IDataMapper DataMapper { get; set; }

        /// <summary>
        /// Provides a means of ensuring "read your own writes" or RYOW consistency on the current query.
        /// </summary>
#pragma warning disable 618
        /// <remarks>Note: <see cref="ScanConsistency"/> will be overwritten to <see cref="Query.ScanConsistency.AtPlus"/>.</remarks>
#pragma warning restore 618
        /// <param name="mutationState">State of the mutation.</param>
        /// <returns>A reference to the current <see cref="IQueryOptions"/> for method chaining.</returns>
        public IQueryOptions ConsistentWith(MutationState mutationState)
        {
#pragma warning disable 618
            WithScanConsistency(ScanConsistency.AtPlus);
#pragma warning restore 618
            _scanVectors = new Dictionary<string, Dictionary<string, List<object>>>();
            foreach (var token in mutationState)
            {
                if (_scanVectors.TryGetValue(token.BucketRef, out var vector))
                {
                    var bucketId = token.VBucketId.ToString();
                    if (vector.TryGetValue(bucketId, out var bucketRef))
                    {
                        if ((long)bucketRef.First() < token.SequenceNumber)
                        {
                            vector[bucketId] = new List<object>
                            {
                                token.SequenceNumber,
                                token.VBucketUuid.ToString()
                            };
                        }
                    }
                    else
                    {
                        vector.Add(token.VBucketId.ToString(),
                            new List<object>
                            {
                                token.SequenceNumber,
                                token.VBucketUuid.ToString()
                            });
                    }
                }
                else
                {
                    _scanVectors.Add(token.BucketRef, new Dictionary<string, List<object>>
                    {
                        {
                            token.VBucketId.ToString(),
                            new List<object>
                            {
                                token.SequenceNumber,
                                token.VBucketUuid.ToString()
                            }
                        }
                    });
                }
            }
            return this;
        }

        /// <summary>
        /// Uses the streaming API for the returned results. This is useful for large result sets in that it limits the
        /// working size of the query and helps reduce the possibility of a <see cref="OutOfMemoryException" /> from occurring.
        /// </summary>
        /// <param name="streaming">if set to <c>true</c> streams the results as you iterate through the response.</param>
        /// <returns>A reference to the current <see cref="QueryOptions"/> for method chaining.</returns>
        public IQueryOptions UseStreaming(bool streaming)
        {
            _useStreaming = streaming;
            return this;
        }

        /// <summary>
        /// Gets a value indicating whether use the <see cref="StreamingQueryClient" />.
        /// </summary>
        /// <value>
        /// <c>true</c> if [use streaming client]; otherwise, <c>false</c>.
        /// </value>
        public bool IsStreaming => _useStreaming.HasValue && _useStreaming.Value;

        /// <summary>
        /// Specifies the maximum parallelism for the query. A zero or negative value means the number of logical
        /// cpus will be used as the parallelism for the query. There is also a server wide max_parallelism parameter
        /// which defaults to 1. If a request includes max_parallelism, it will be capped by the server max_parallelism.
        /// If a request does not include max_parallelism, the server wide max_parallelism will be used.
        /// </summary>
        /// <param name="parallelism"></param>
        /// <returns></returns>
        /// <value>
        /// The maximum server parallelism.
        /// </value>
        public IQueryOptions MaxServerParallelism(int parallelism)
        {
            _maxServerParallelism = parallelism;
            return this;
        }

        /// <summary>
        /// If set to false, the client will try to perform optimizations
        /// transparently based on the server capabilities, like preparing the statement and
        /// then executing a query plan instead of the raw query.
        /// </summary>
        /// <param name="adHoc">if set to <c>false</c> the query will be optimized if possible.</param>
        /// <returns></returns>
        /// <remarks>
        /// The default is <c>true</c>; the query will executed in an ad-hoc manner,
        /// without special optomizations.
        /// </remarks>
        public IQueryOptions AdHoc(bool adHoc)
        {
            _adHoc = adHoc;
            return this;
        }

        /// <summary>
        ///  Sets a N1QL statement to be executed in an optimized way using the given queryPlan.
        /// </summary>
        /// <param name="preparedPlan">The <see cref="QueryPlan"/> that was prepared beforehand.</param>
        /// <param name="originalStatement">The original statement (eg. SELECT * FROM default) that the user attempted to optimize</param>
        /// <returns>A reference to the current <see cref="QueryOptions"/> for method chaining.</returns>
        /// <remarks>Required if statement not provided, will erase a previously set Statement.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="preparedPlan"/> is <see langword="null" />.</exception>
        public IQueryOptions Prepared(QueryPlan preparedPlan, string originalStatement)
        {
            if (preparedPlan == null || string.IsNullOrWhiteSpace(preparedPlan.EncodedPlan))
            {
                throw new ArgumentNullException(nameof(preparedPlan));
            }
            if (string.IsNullOrWhiteSpace(originalStatement))
            {
                throw new ArgumentNullException(nameof(originalStatement));
            }
            _statement = originalStatement;
            _preparedPayload = preparedPlan;
            _prepareEncoded = true;
            return this;
        }

        /// <summary>
        /// Sets a N1QL statement to be executed.
        /// </summary>
        /// <param name="statement">Any valid N1QL statement for a POST request, or a read-only N1QL statement (SELECT, EXPLAIN) for a GET request.</param>
        /// <returns>
        /// A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">statement</exception>
        /// <remarks>
        /// Will erase a previous optimization of a statement using Prepared.
        /// </remarks>
        public IQueryOptions Statement(string statement)
        {
            if (string.IsNullOrWhiteSpace(statement)) throw new ArgumentNullException(nameof(statement));
            _statement = statement;
            _preparedPayload = null;
            _prepareEncoded = false;
            return this;
        }

        /// <summary>
        /// Sets the maximum time to spend on the request.
        /// </summary>
        /// <param name="timeOut">Maximum time to spend on the request</param>
        /// <returns>
        /// A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        /// <remarks>
        /// Optional - the default is 0ms, which means the request runs for as long as it takes.
        /// </remarks>
        public IQueryOptions Timeout(TimeSpan timeOut)
        {
            _timeOut = timeOut;
            return this;
        }

        /// <summary>
        /// If a GET request, this will always be true otherwise false.
        /// </summary>
        /// <param name="readOnly">True for get requests.</param>
        /// <returns>
        /// A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        /// <remarks>
        /// Any value set here will be overridden by the type of request sent.
        /// </remarks>
        public IQueryOptions ReadOnly(bool readOnly)
        {
            _readOnly = readOnly;
            return this;
        }

        /// <summary>
        /// Specifies that metrics should be returned with query results.
        /// </summary>
        /// <param name="includeMetrics">True to return query metrics.</param>
        /// <returns>
        /// A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        /// <remarks>
        /// Optional.
        /// </remarks>
        public IQueryOptions Metrics(bool includeMetrics)
        {
            _includeMetrics = includeMetrics;
            return this;
        }

        /// <summary>
        /// Adds a named parameter to the parameters to the statement or prepared statement.
        /// </summary>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="value">The value of the parameter.</param>
        /// <returns>
        /// A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        /// <remarks>
        /// Optional.
        /// </remarks>
        public IQueryOptions AddNamedParameter(string name, object value)
        {
            _parameters.Add(name, value);
            return this;
        }

        /// <summary>
        /// Adds a positional parameter to the parameters to the statement or prepared statement.
        /// </summary>
        /// <param name="value">The value of the positional parameter.</param>
        /// <returns>
        /// A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        /// <remarks>
        /// Optional.
        /// </remarks>
        public IQueryOptions AddPositionalParameter(object value)
        {
            _arguments.Add(value);
            return this;
        }

        /// <summary>
        /// Adds a collection of named parameters to the parameters to the statement or prepared statement.
        /// </summary>
        /// <param name="parameters">A list of <see cref="KeyValuePair{K, V}" /> to be sent.</param>
        /// <returns>
        /// A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        /// <remarks>
        /// Optional.
        /// </remarks>
        public IQueryOptions AddNamedParameter(params KeyValuePair<string, object>[] parameters)
        {
            foreach (var parameter in parameters)
            {
                _parameters.Add(parameter.Key, parameter.Value);
            }
            return this;
        }

        /// <summary>
        /// Adds a list of positional parameters to the statement or prepared statement.
        /// </summary>
        /// <param name="parameters">A list of positional parameters.</param>
        /// <returns></returns>
        public IQueryOptions AddPositionalParameter(params object[] parameters)
        {
            foreach (var parameter in parameters)
            {
                _arguments.Add(parameter);
            }
            return this;
        }

        /// <summary>
        /// Desired format for the query results.
        /// </summary>
        /// <param name="format">An <see cref="Format" /> enum.</param>
        /// <returns>
        /// A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        /// <remarks>
        /// Optional.
        /// </remarks>
        public IQueryOptions Format(Format format)
        {
            _format = format;
            return this;
        }

        /// <summary>
        /// Specifies the desired character encoding for the query results.
        /// </summary>
        /// <param name="encoding">An <see cref="Encoding" /> enum.</param>
        /// <returns>
        /// A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        /// <remarks>
        /// Optional.
        /// </remarks>
        public IQueryOptions Encoding(Encoding encoding)
        {
            _encoding = encoding;
            return this;
        }

        /// <summary>
        /// Compression format to use for response data on the wire. Possible values are ZIP, RLE, LZMA, LZO, NONE.
        /// </summary>
        /// <param name="compression"></param>
        /// <returns>
        /// A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        /// <remarks>
        /// Optional. The default is NONE.
        /// </remarks>
        public IQueryOptions Compression(Compression compression)
        {
            _compression = compression;
            return this;
        }

        /// <summary>
        /// Includes a header for the results schema in the response.
        /// </summary>
        /// <param name="includeSignature">True to include a header for the results schema in the response.</param>
        /// <returns>
        /// A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        /// <remarks>
        /// The default is true.
        /// </remarks>
        public IQueryOptions Signature(bool includeSignature)
        {
            _includeSignature = includeSignature;
            return this;
        }

        /// <summary>
        /// Specifies the consistency guarantee/constraint for index scanning.
        /// </summary>
        /// <param name="scanConsistency">Specify the consistency guarantee/constraint for index scanning.</param>
        /// <returns>
        /// A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        /// <exception cref="NotSupportedException">StatementPlus are not currently supported by CouchbaseServer.</exception>
        /// <remarks>
        /// Optional.
        /// </remarks>
        public IQueryOptions WithScanConsistency(ScanConsistency scanConsistency)
        {
#pragma warning disable 618
            if (scanConsistency == ScanConsistency.StatementPlus)
#pragma warning restore 618
            {
                throw new NotSupportedException(
                    "AtPlus and StatementPlus are not currently supported by CouchbaseServer.");
            }
            _scanConsistency = scanConsistency;
            return this;
        }

        /// <summary>
        /// Specifies the maximum time the client is willing to wait for an index to catch up to the vector timestamp in the request. If an index has to catch up, and the <see cref="ScanWait" /> time is exceed doing so, an error is returned.
        /// </summary>
        /// <param name="scanWait">The maximum time the client is willing to wait for index to catch up to the vector timestamp.</param>
        /// <returns>
        /// A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        /// <remarks>
        /// Optional.
        /// </remarks>
        public IQueryOptions ScanWait(TimeSpan scanWait)
        {
            _scanWait = scanWait;
            return this;
        }

        /// <summary>
        /// Pretty print the output.
        /// </summary>
        /// <param name="pretty">True for the pretty.</param>
        /// <returns>
        /// A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        /// <remarks>
        /// True by default.
        /// </remarks>
        public IQueryOptions Pretty(bool pretty)
        {
            _pretty = pretty;
            return this;
        }

        /// <summary>
        /// Adds a set of credentials to the list of credentials, in the form of user/password
        /// </summary>
        /// <param name="username">The bucket or username.</param>
        /// <param name="password">The password of the bucket.</param>
        /// <param name="isAdmin">True if connecting as an admin.</param>
        /// <returns>
        /// A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        /// <exception cref="System.ArgumentOutOfRangeException">username;cannot be null, empty or whitespace.</exception>
        /// <remarks>
        /// Optional.
        /// </remarks>
        public IQueryOptions AddCredentials(string username, string password, bool isAdmin)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                const string usernameParameter = "username";
                throw new ArgumentOutOfRangeException(username, ExceptionUtil.GetMessage(ExceptionUtil.ParameterCannotBeNullOrEmptyFormat, usernameParameter));
            }
            if (isAdmin && !username.StartsWith("admin:"))
            {
                username = "admin:" + username;
            }
            else if(!username.StartsWith("local:"))
            {
                username = "local:" + username;
            }
            _credentials[username] = password;
            return this;
        }

        /// <summary>
        /// Clients the context identifier.
        /// </summary>
        /// <param name="clientContextId">The client context identifier.</param>
        /// <returns></returns>
        public IQueryOptions ClientContextId(string clientContextId)
        {
            //this is seeded in the ctor
            if (clientContextId != null)
            {
                _clientContextId = clientContextId;
            }
            return this;
        }

        /// <summary>
        /// Bases the URI.
        /// </summary>
        /// <param name="baseUri">The base URI.</param>
        /// <returns></returns>
        public IQueryOptions BaseUri(Uri baseUri)
        {
            _baseUri = baseUri;
            return this;
        }

        /// <summary>
        /// Adds a raw query parameter and value to the query.
        /// NOTE: This is uncommited and may change in the future.
        /// </summary>
        /// <param name="name">The paramter name.</param>
        /// <param name="value">The parameter value.</param>
        /// <returns>A reference to the current <see cref="QueryOptions" /> for method chaining.</returns>
        public IQueryOptions RawParameter(string name, object value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Parameter name cannot be null or empty.");
            }

            _rawParameters.Add(name, value);
            return this;
        }

        /// <summary>
        /// Sets maximum buffered channel size between the indexer client
        /// and the query service for index scans.
        ///
        /// This parameter controls when to use scan backfill.
        /// Use 0 or a negative number to disable.
        /// </summary>
        /// <param name="capacity">The maximum number of channels.</param>
        /// <returns>
        /// A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        public IQueryOptions ScanCapacity(int capacity)
        {
            _scanCapacity = capacity;
            return this;
        }

        /// <summary>
        /// Sets the number of items execution operators can batch for
        /// fetch from the KV.
        /// </summary>
        /// <param name="batchSize">The maximum number of items.</param>
        /// <returns>
        /// A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        public IQueryOptions PipelineBatch(int batchSize)
        {
            _pipelineBatch = batchSize;
            return this;
        }

        /// <summary>
        /// Sets maximum number of items each execution operator can buffer
        /// between various operators.
        /// </summary>
        /// <param name="capacity">The maximum number of items.</param>
        /// <returns>
        /// A reference to the current <see cref="QueryOptions" /> for method chaining.
        /// </returns>
        public IQueryOptions PipelineCapacity(int capacity)
        {
            _pipelineCapacity = capacity;
            return this;
        }

        public IQueryOptions Profile(QueryProfile profile)
        {
            _profile = profile;
            return this;
        }

        public Uri GetBaseUri()
        {
            return _baseUri;
        }

        /// <summary>
        /// Gets the raw, unprepared N1QL statement.
        /// </summary>
        /// <remarks>If the statement has been optimized using Prepared, this will still
        /// return the original un-optimized statement.</remarks>
        public string GetOriginalStatement()
        {
            return _statement;
        }

        /// <summary>
        /// Gets the prepared payload for this N1QL statement if IsPrepared() is true,
        /// null otherwise.
        /// </summary>
        public QueryPlan GetPreparedPayload()
        {
            return _preparedPayload;
        }

        /// <summary>
        /// Gets a <see cref="IDictionary{K, V}" /> of the name/value pairs to be POSTed to the service.
        /// </summary>
        /// <returns>
        /// The <see cref="IDictionary{K, V}" /> of the name/value pairs to be POSTed to the service.
        /// </returns>
        /// <exception cref="System.ArgumentException">A statement or prepared plan must be provided.</exception>
        /// <remarks>Since values will be POSTed as JSON, here we deal with unencoded typed values
        /// (like ints, Lists, etc...) rather than only strings.</remarks>
        public IDictionary<string, object> GetFormValues()
        {
            return GetFormValues(true);
        }

        private IDictionary<string, object> GetFormValues(bool generateNewId)
        {
            if (string.IsNullOrWhiteSpace(_statement) ||
                (_prepareEncoded && _preparedPayload == null))
            {
                throw new ArgumentException("A statement or prepared plan must be provided.");
            }

            //build the map of request parameters
            IDictionary<string, object> formValues = new Dictionary<string, object>();

            if (_maxServerParallelism.HasValue)
            {
                formValues.Add(QueryParameters.MaxServerParallelism, _maxServerParallelism.Value.ToString());
            }
            if (_prepareEncoded)
            {
                formValues.Add(QueryParameters.Prepared, _preparedPayload.Name);
                formValues.Add(QueryParameters.PreparedEncoded, _preparedPayload.EncodedPlan);
            }
            else
            {
                formValues.Add(QueryParameters.Statement, _statement);
            }

            if (_timeOut.HasValue && _timeOut.Value > TimeSpan.Zero)
            {
                formValues.Add(QueryParameters.Timeout, (uint) _timeOut.Value.TotalMilliseconds + "ms");
            }
            else
            {
                formValues.Add(QueryParameters.Timeout, string.Concat(TimeoutDefault, "ms"));
            }
            if (_readOnly.HasValue)
            {
                formValues.Add(QueryParameters.Readonly, _readOnly.Value);
            }
            if (_includeMetrics.HasValue)
            {
                formValues.Add(QueryParameters.Metrics, _includeMetrics);
            }
            if (_parameters.Count > 0)
            {
                foreach (var parameter in _parameters)
                {
                    formValues.Add(
                        parameter.Key.Contains("$") ? parameter.Key : "$" + parameter.Key,
                        parameter.Value);
                }
            }
            if (_arguments.Count > 0)
            {
                formValues.Add(QueryParameters.Args, _arguments);
            }
            if (_format.HasValue)
            {
                formValues.Add(QueryParameters.Format, _format.Value.ToString());
            }
            if (_encoding.HasValue)
            {
                formValues.Add(QueryParameters.Encoding, _encoding.Value.ToString());
            }
            if (_compression.HasValue)
            {
                formValues.Add(QueryParameters.Compression, _compression.Value.ToString());
            }
            if (_includeSignature.HasValue)
            {
                formValues.Add(QueryParameters.Signature, _includeSignature.Value);
            }
            if (_scanConsistency.HasValue)
            {
                formValues.Add(QueryParameters.ScanConsistency, _scanConsistency.GetDescription());
            }
            if (_scanVectors != null)
            {
#pragma warning disable 618
                if (_scanConsistency != ScanConsistency.AtPlus)
#pragma warning restore 618
                {
                    throw new ArgumentException("Only ScanConsistency.AtPlus is supported for this query request.");
                }
                formValues.Add(QueryParameters.ScanVectors, _scanVectors);
            }
            if (_scanWait.HasValue)
            {
                formValues.Add(QueryParameters.ScanWait, string.Format("{0}ms", (uint) _scanWait.Value.TotalMilliseconds));
            }
            if (_pretty != null)
            {
                formValues.Add(QueryParameters.Pretty, _pretty.Value);
            }
            if (_credentials.Count > 0)
            {
                var creds = new List<dynamic>();
                foreach (var credential in _credentials)
                {
                    creds.Add(new { user = credential.Key, pass = credential.Value });
                }
                formValues.Add(QueryParameters.Creds, creds);
            }
            if (_scanCapacity.HasValue)
            {
                formValues.Add(QueryParameters.ScanCapacity, _scanCapacity.Value.ToString());
            }
            if (_pipelineBatch.HasValue)
            {
                formValues.Add(QueryParameters.PipelineBatch, _pipelineBatch.Value.ToString());
            }
            if (_pipelineCapacity.HasValue)
            {
                formValues.Add(QueryParameters.PipelineCapacity, _pipelineCapacity.Value.ToString());
            }
            if (generateNewId)
            {
                _requestContextId = QuerySequenceGenerator.GetNext();
            }
            if (_profile != QueryProfile.Off)
            {
                formValues.Add(QueryParameters.Profile, _profile.ToString().ToLowerInvariant());
            }
            foreach (var parameter in _rawParameters)
            {
                formValues.Add(parameter.Key, parameter.Value);
            }

            formValues.Add(QueryParameters.ClientContextId, CurrentContextId);
            return formValues;
        }

        /// <summary>
        /// Gets the query parameters for x-form-urlencoded content-type.
        /// </summary>
        /// <remarks>Each key and value from GetFormValues will be urlencoded</remarks>
        /// <returns></returns>
        [Obsolete("JSON method is used instead of x-form-urlencoded")]
        public string GetQueryParametersAsFormUrlencoded()
        {
            var sb = new StringBuilder();
            var formValues = GetFormValues();
            foreach (var formValue in GetFormValues())
            {
                sb.AppendFormat(QueryArgPattern,
                    WebUtility.UrlEncode(formValue.Key),
                    WebUtility.UrlEncode(formValue.Value.ToString()));
            }
            if (formValues.Count > 0)
            {
                sb.Remove(sb.Length - 1, 1);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Gets the JSON representation of this query for execution in a POST.
        /// </summary>
        /// <returns>The form values as a JSON object.</returns>
        public string GetFormValuesAsJson()
        {
            return GetFormValuesAsJson(true);
        }

        private string GetFormValuesAsJson(bool generateId)
        {
            var formValues = GetFormValues(generateId);
            return JsonConvert.SerializeObject(formValues);
        }

        /// <summary>
        /// Creates a new <see cref="QueryOptions"/> object.
        /// </summary>
        /// <returns></returns>
        public static IQueryOptions Create()
        {
            return new QueryOptions();
        }

        /// <summary>
        /// Creates a new <see cref="QueryOptions"/> object with the specified statement.
        /// </summary>
        /// <param name="statement">The statement.</param>
        /// <returns></returns>
        public static IQueryOptions Create(string statement)
        {
            return new QueryOptions(statement);
        }

        /// <summary>
        /// Creates a query using the given plan as an optimization for the originalStatement.
        /// </summary>
        /// <param name="plan">The plan.</param>
        /// <param name="originalStatement">The original statement, unoptimized.</param>
        /// <returns></returns>
        public static IQueryOptions Create(QueryPlan plan, string originalStatement)
        {
            return new QueryOptions(plan, originalStatement);
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            string request;
            try
            {
                request = GetBaseUri() + "[" + GetFormValuesAsJson(false) + "]";
            }
            catch
            {
                request = string.Empty;
            }
            return request;
        }

        /// <summary>
        /// Sets the lifespan of the query request; used to check if the request exceeded the maximum time
#pragma warning disable 1584,1711,1572,1581,1580
        /// configured for it in <see cref="ClientConfiguration.QueryRequestTimeout" />
#pragma warning restore 1584,1711,1572,1581,1580
        /// </summary>
        /// <value>
        /// The lifespan.
        /// </value>
        Lifespan IQueryOptions.Lifespan { get; set; }

        /// <summary>
#pragma warning disable 1584,1711,1572,1581,1580
        /// True if the request exceeded it's <see cref="ClientConfiguration.QueryRequestTimeout" />
#pragma warning restore 1584,1711,1572,1581,1580
        /// </summary>
        /// <returns></returns>
        bool IQueryOptions.TimedOut()
        {
            var temp = this as IQueryOptions;
            return temp.Lifespan.TimedOut();
        }

        /// <summary>
        /// Gets or sets the timeout value of the <see cref="QueryOptions"/> in microseconds.
        /// </summary>
        internal uint TimeoutValue
        {
            get
            {
                var temp = this as IQueryOptions;
                return temp.Lifespan.Duration * 1000;
            }
        }
    }
}
