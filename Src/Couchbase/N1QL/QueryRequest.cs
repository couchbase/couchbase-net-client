using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;

namespace Couchbase.N1QL
{
    /// <summary>
    /// Builds a N1QL query request.
    /// </summary>
    public class QueryRequest : IQueryRequest
    {
        private Method _method;
        private string _statement;
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
        private dynamic _scanVector;
        private TimeSpan? _scanWait;
        private bool _pretty = false;
        private readonly Dictionary<string, string> _credentials = new Dictionary<string, string>();
        private string _clientContextId;
        private Uri _baseUri;
        private bool _prepared;

        public const string ForwardSlash = "/";
        public const string QueryOperator = "?";
        private const string QueryArgPattern = "{0}={1}&";
        private const string ParameterIdentifier = "$";
        private const string LowerCaseTrue = "true";
        private const string LowerCaseFalse = "false";
        public const string TimeoutArgPattern = "{0}={1}ms&";

        public static readonly Dictionary<ScanConsistency, string> ScanConsistencyResolver = new Dictionary<ScanConsistency, string>
        {
            {N1QL.ScanConsistency.AtPlus, "at_plus"},
            {N1QL.ScanConsistency.NotBounded, "not_bounded"},
            {N1QL.ScanConsistency.RequestPlus, "request_plus"},
            {N1QL.ScanConsistency.StatementPlus, "statement_plus"}
        };

        public QueryRequest()
        {
        }

        public QueryRequest(string statement)
        {
            _statement = statement;
            _prepared = false;
        }

        public QueryRequest(IQueryPlan plan)
        {
            _statement = plan.ToN1ql();
            _prepared = true;
        }

        private struct QueryParameters
        {
            public const string Statement = "statement";
            public const string Prepared = "prepared";
            public const string Timeout = "timeout";
            public const string Readonly = "readonly";
            public const string Metrics = "metrics";
            public const string Args = "args";
            public const string BatchArgs = "batch_args";
            public const string BatchNamedArgs = "batch_named_args";
            public const string Format = "format";
            public const string Encoding = "encoding";
            public const string Compression = "compression";
            public const string Signature = "signature";
            public const string ScanConsistency = "scan_consistency";
            public const string ScanVector = "scan_vector";
            public const string ScanWait = "scan_wait";
            public const string Pretty = "pretty";
            public const string Creds = "creds";
            public const string ClientContextId = "client_context_id";
        }

        public bool IsPost
        {
            get { return _method == N1QL.Method.Post; }
        }

        public bool IsPrepared
        {
            get { return _prepared; }
        }

        /// <summary>
        /// Prepareds the specified prepared plan.
        /// </summary>
        /// <param name="preparedPlan">The prepared plan.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">preparedPlan</exception>
        public IQueryRequest Prepared(IQueryPlan preparedPlan)
        {
            if (preparedPlan == null || string.IsNullOrWhiteSpace(preparedPlan.ToN1ql()))
            {
                throw new ArgumentNullException("preparedPlan");
            }
            _statement = preparedPlan.ToN1ql();
            _prepared = true;
            return this;
        }

        /// <summary>
        /// The HTTP method type to use.
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public IQueryRequest HttpMethod(Method method)
        {
            _method = method;
            return this;
        }

        /// <summary>
        /// Sets a N1QL statement to be executed.
        /// </summary>
        /// <param name="statement">Any valid N1QL statement for a POST request, or a read-only N1QL statement (SELECT, EXPLAIN) for a GET request.</param>
        /// <returns>
        /// A reference to the current <see cref="IQueryRequest" /> for method chaining.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">statement</exception>
        /// <remarks>
        /// If both prepared and statement are present and non-empty, an error is returned.
        /// </remarks>
        public IQueryRequest Statement(string statement)
        {
            if (string.IsNullOrWhiteSpace(statement))
            {
                throw new ArgumentNullException("statement");
            }
            _statement = statement;
            _prepared = false;
            return this;
        }

        /// <summary>
        /// Sets the maximum time to spend on the request.
        /// </summary>
        /// <param name="timeOut">Maximum time to spend on the request</param>
        /// <returns>
        /// A reference to the current <see cref="IQueryRequest" /> for method chaining.
        /// </returns>
        /// <remarks>
        /// Optional - the default is 0ms, which means the request runs for as long as it takes.
        /// </remarks>
        public IQueryRequest Timeout(TimeSpan timeOut)
        {
            _timeOut = timeOut;
            return this;
        }

        /// <summary>
        /// If a GET request, this will always be true otherwise false.
        /// </summary>
        /// <param name="readOnly">True for get requests.</param>
        /// <returns>
        /// A reference to the current <see cref="IQueryRequest" /> for method chaining.
        /// </returns>
        /// <remarks>
        /// Any value set here will be overridden by the type of request sent.
        /// </remarks>
        public IQueryRequest ReadOnly(bool readOnly)
        {
            _readOnly = readOnly;
            return this;
        }

        /// <summary>
        /// Specifies that metrics should be returned with query results.
        /// </summary>
        /// <param name="includeMetrics">True to return query metrics.</param>
        /// <returns>
        /// A reference to the current <see cref="IQueryRequest" /> for method chaining.
        /// </returns>
        /// <remarks>
        /// Optional.
        /// </remarks>
        public IQueryRequest Metrics(bool includeMetrics)
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
        /// A reference to the current <see cref="IQueryRequest" /> for method chaining.
        /// </returns>
        /// <remarks>
        /// Optional.
        /// </remarks>
        public IQueryRequest AddNamedParameter(string name, object value)
        {
            _parameters.Add(name, value);
            return this;
        }

        /// <summary>
        /// Adds a positional parameter to the parameters to the statement or prepared statement.
        /// </summary>
        /// <param name="value">The value of the positional parameter.</param>
        /// <returns>
        /// A reference to the current <see cref="IQueryRequest" /> for method chaining.
        /// </returns>
        /// <remarks>
        /// Optional.
        /// </remarks>
        public IQueryRequest AddPositionalParameter(object value)
        {
            _arguments.Add(value);
            return this;
        }

        /// <summary>
        /// Adds a collection of named parameters to the parameters to the statement or prepared statement.
        /// </summary>
        /// <param name="parameters">A list of <see cref="KeyValuePair{K, V}" /> to be sent.</param>
        /// <returns>
        /// A reference to the current <see cref="IQueryRequest" /> for method chaining.
        /// </returns>
        /// <remarks>
        /// Optional.
        /// </remarks>
        public IQueryRequest AddNamedParameter(params KeyValuePair<string, object>[] parameters)
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
        public IQueryRequest AddPositionalParameter(params object[] parameters)
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
        /// A reference to the current <see cref="IQueryRequest" /> for method chaining.
        /// </returns>
        /// <remarks>
        /// Optional.
        /// </remarks>
        public IQueryRequest Format(Format format)
        {
            _format = format;
            return this;
        }

        /// <summary>
        /// Specifies the desired character encoding for the query results.
        /// </summary>
        /// <param name="encoding">An <see cref="Encoding" /> enum.</param>
        /// <returns>
        /// A reference to the current <see cref="IQueryRequest" /> for method chaining.
        /// </returns>
        /// <remarks>
        /// Optional.
        /// </remarks>
        public IQueryRequest Encoding(Encoding encoding)
        {
            _encoding = encoding;
            return this;
        }

        /// <summary>
        /// Compression format to use for response data on the wire. Possible values are ZIP, RLE, LZMA, LZO, NONE.
        /// </summary>
        /// <param name="compression"></param>
        /// <returns>
        /// A reference to the current <see cref="IQueryRequest" /> for method chaining.
        /// </returns>
        /// <remarks>
        /// Optional. The default is NONE.
        /// </remarks>
        public IQueryRequest Compression(Compression compression)
        {
            _compression = compression;
            return this;
        }

        /// <summary>
        /// Includes a header for the results schema in the response.
        /// </summary>
        /// <param name="includeSignature">True to include a header for the results schema in the response.</param>
        /// <returns>
        /// A reference to the current <see cref="IQueryRequest" /> for method chaining.
        /// </returns>
        /// <remarks>
        /// The default is true.
        /// </remarks>
        public IQueryRequest Signature(bool includeSignature)
        {
            _includeSignature = includeSignature;
            return this;
        }

        /// <summary>
        /// Specifies the consistency guarantee/constraint for index scanning.
        /// </summary>
        /// <param name="scanConsistency">Specify the consistency guarantee/constraint for index scanning.</param>
        /// <returns>
        /// A reference to the current <see cref="IQueryRequest" /> for method chaining.
        /// </returns>
        /// <exception cref="System.NotSupportedException">AtPlus and StatementPlus are not currently supported by CouchbaseServer.</exception>
        /// <exception cref="NotSupportedException">AtPlus and StatementPlus are not currently supported by CouchbaseServer.</exception>
        /// <remarks>
        /// Optional.
        /// </remarks>
        public IQueryRequest ScanConsistency(ScanConsistency scanConsistency)
        {
            if (scanConsistency == N1QL.ScanConsistency.AtPlus ||
                scanConsistency == N1QL.ScanConsistency.StatementPlus)
            {
                throw new NotSupportedException(
                    "AtPlus and StatementPlus are not currently supported by CouchbaseServer.");
            }
            _scanConsistency = scanConsistency;
            return this;
        }

        /// <summary>
        /// Scans the vector.
        /// </summary>
        /// <param name="scanVector">The scan vector.</param>
        /// <returns></returns>
        public IQueryRequest ScanVector(dynamic scanVector)
        {
            _scanVector = scanVector;
            return this;
        }

        /// <summary>
        /// Specifies the maximum time the client is willing to wait for an index to catch up to the vector timestamp in the request. If an index has to catch up, and the <see cref="ScanWait" /> time is exceed doing so, an error is returned.
        /// </summary>
        /// <param name="scanWait">The maximum time the client is willing to wait for index to catch up to the vector timestamp.</param>
        /// <returns>
        /// A reference to the current <see cref="IQueryRequest" /> for method chaining.
        /// </returns>
        /// <remarks>
        /// Optional.
        /// </remarks>
        public IQueryRequest ScanWait(TimeSpan scanWait)
        {
            _scanWait = scanWait;
            return this;
        }

        /// <summary>
        /// Pretty print the output.
        /// </summary>
        /// <param name="pretty">True for the pretty.</param>
        /// <returns>
        /// A reference to the current <see cref="IQueryRequest" /> for method chaining.
        /// </returns>
        /// <remarks>
        /// True by default.
        /// </remarks>
        public IQueryRequest Pretty(bool pretty)
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
        /// A reference to the current <see cref="IQueryRequest" /> for method chaining.
        /// </returns>
        /// <exception cref="System.ArgumentOutOfRangeException">username;cannot be null, empty or whitespace.</exception>
        /// <remarks>
        /// Optional.
        /// </remarks>
        public IQueryRequest AddCredentials(string username, string password, bool isAdmin)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentOutOfRangeException("username", "cannot be null, empty or whitespace.");
            }
            if (isAdmin && !username.StartsWith("admin:"))
            {
                username = "admin:" + username;
            }
            else if(!username.StartsWith("local:"))
            {
                username = "local:" + username;
            }
            _credentials.Add(username, password);
            return this;
        }

        /// <summary>
        /// Clients the context identifier.
        /// </summary>
        /// <param name="clientContextId">The client context identifier.</param>
        /// <returns></returns>
        public IQueryRequest ClientContextId(string clientContextId)
        {
            _clientContextId = clientContextId;
            return this;
        }

        /// <summary>
        /// Bases the URI.
        /// </summary>
        /// <param name="baseUri">The base URI.</param>
        /// <returns></returns>
        public IQueryRequest BaseUri(Uri baseUri)
        {
            _baseUri = baseUri;
            return this;
        }

        /// <summary>
        /// Checks the HTTP method.
        /// </summary>
        void CheckMethod()
        {
            if (_method != Method.None) return;
            if (!string.IsNullOrWhiteSpace(_statement))
            {
                var statement = _statement.ToLower();
                _method = statement.Contains("SELECT") || statement.Contains("select")
                    ? Method.Get
                    : Method.Post;
            }
        }

        /// <summary>
        /// Gets the constructed <see cref="Uri" /> for making the request.
        /// </summary>
        /// <returns>
        /// A reference to the current <see cref="IQueryRequest" /> for method chaining.
        /// </returns>
        /// <exception cref="System.ArgumentException">A statement or prepared plan must be provided.</exception>
        public Uri GetRequestUri()
        {
            if (string.IsNullOrWhiteSpace(_statement))
            {
                throw new ArgumentException("A statement or prepared plan must be provided.");
            }
            CheckMethod();

            //build the request query starting with the base uri- e.g. http://localhost:8093/query
            var sb = new StringBuilder();
            sb.Append(_baseUri + "?");

            if (_prepared)
            {
                sb.AppendFormat(QueryArgPattern, QueryParameters.Prepared, _statement);
            }
            else
            {
                sb.AppendFormat(QueryArgPattern, QueryParameters.Statement, _statement);
            }
            if (_timeOut.HasValue && _timeOut.Value > TimeSpan.Zero)
            {
                sb.AppendFormat(TimeoutArgPattern, QueryParameters.Timeout,
                    EncodeParameter((uint)_timeOut.Value.TotalMilliseconds));
            }
            if (_readOnly.HasValue)
            {
                sb.AppendFormat(QueryArgPattern, QueryParameters.Readonly, _readOnly.Value ? LowerCaseTrue : LowerCaseFalse);
            }
            if (_includeMetrics.HasValue)
            {
                sb.AppendFormat(QueryArgPattern, QueryParameters.Metrics, _includeMetrics.Value ? LowerCaseTrue : LowerCaseFalse);
            }
            if (_parameters.Count > 0)
            {
                foreach (var parameter in _parameters)
                {
                    sb.AppendFormat(QueryArgPattern,
                       parameter.Key.Contains("$") ? parameter.Key : "$" + parameter.Key,
                       EncodeParameter( parameter.Value));
                }
            }
            if (_arguments.Count > 0)
            {
                sb.AppendFormat(QueryArgPattern, QueryParameters.Args, EncodeParameter(_arguments));
            }
            if (_format.HasValue)
            {
                sb.AppendFormat(QueryArgPattern, QueryParameters.Format, _format);
            }
            if (_encoding.HasValue)
            {
                sb.AppendFormat(QueryArgPattern, QueryParameters.Encoding, _encoding);
            }
            if (_compression.HasValue)
            {
                sb.AppendFormat(QueryArgPattern, QueryParameters.Compression, _compression);
            }
            if(_includeSignature.HasValue)
            {
                sb.AppendFormat(QueryArgPattern, QueryParameters.Signature, _includeSignature.Value ? LowerCaseTrue : LowerCaseFalse);
            }
            if (_scanConsistency.HasValue)
            {
                sb.AppendFormat(QueryArgPattern, QueryParameters.ScanConsistency, ScanConsistencyResolver[_scanConsistency.Value]);
            }
            if (_scanVector != null)
            {
                sb.AppendFormat(QueryArgPattern, QueryParameters.ScanVector, _scanVector);
            }
            if (_scanWait.HasValue)
            {
                sb.AppendFormat(QueryArgPattern, QueryParameters.ScanWait, "" + ((uint)_scanWait.Value.TotalMilliseconds));
            }
            if (_pretty)
            {
                sb.AppendFormat(QueryArgPattern, QueryParameters.Pretty, _pretty ? LowerCaseTrue : LowerCaseFalse);
            }
            if (_credentials.Count > 0)
            {
                var creds = new List<dynamic>();
                foreach (var credential in _credentials)
                {
                    creds.Add(new {user=credential.Key, pass=credential.Value});
                }
                sb.AppendFormat(QueryArgPattern, QueryParameters.Creds, EncodeParameter(creds));
            }
            if (!string.IsNullOrEmpty(_clientContextId))
            {
                sb.AppendFormat(QueryArgPattern, QueryParameters.ClientContextId, _clientContextId);
            }
            return new Uri(sb.ToString().TrimEnd('&'));
        }

        public Uri GetBaseUri()
        {
            return _baseUri;
        }

        public string GetStatement()
        {
            return _statement;
        }

        /// <summary>
        /// Gets a <see cref="IDictionary{K, V}" /> of the name/value pairs to be POSTed to the service if <see cref="Method.Post" /> is used.
        /// </summary>
        /// <returns>
        /// The <see cref="IDictionary{K, V}" /> of the name/value pairs to be POSTed to the service.
        /// </returns>
        /// <exception cref="System.ArgumentException">A statement or prepared plan must be provided.</exception>
        public IDictionary<string, string> GetFormValues()
        {
            if (string.IsNullOrWhiteSpace(_statement))
            {
                throw new ArgumentException("A statement or prepared plan must be provided.");
            }
            CheckMethod();

            //build the request query starting with the base uri- e.g. http://localhost:8093/query
            IDictionary<string, string> formValues = new Dictionary<string, string>();

            if (_prepared)
            {
                formValues.Add(QueryParameters.Prepared, _statement);
            }
            else
            {
                formValues.Add(QueryParameters.Statement, _statement);
            }

            if (_timeOut.HasValue && _timeOut.Value > TimeSpan.Zero)
            {
                formValues.Add(QueryParameters.Timeout, (uint)_timeOut.Value.TotalMilliseconds + "ms");
            }
            if (_readOnly.HasValue)
            {
                formValues.Add(QueryParameters.Readonly, _readOnly.Value ? LowerCaseTrue : LowerCaseFalse);
            }
            if (_includeMetrics.HasValue)
            {
                formValues.Add(QueryParameters.Metrics, _includeMetrics.Value ? LowerCaseTrue : LowerCaseFalse);
            }
            if (_parameters.Count > 0)
            {
                foreach (var parameter in _parameters)
                {
                    formValues.Add(
                        parameter.Key.Contains("$") ? parameter.Key : "$" + parameter.Key,
                        JsonConvert.SerializeObject(parameter.Value));
                }
            }
            if (_arguments.Count > 0)
            {
                formValues.Add(QueryParameters.Args, EncodeParameter(_arguments));
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
                formValues.Add(QueryParameters.Signature, _includeSignature.Value ? LowerCaseTrue : LowerCaseFalse);
            }
            if (_scanConsistency.HasValue)
            {
                formValues.Add(QueryParameters.ScanConsistency, ScanConsistencyResolver[_scanConsistency.Value]);
            }
            if (_scanVector != null)
            {
                formValues.Add(QueryParameters.ScanVector, _scanVector);
            }
            if (_scanWait.HasValue)
            {
                formValues.Add(QueryParameters.ScanWait, "" + ((uint) _scanWait.Value.TotalMilliseconds));
            }
            if (_pretty)
            {
                formValues.Add(QueryParameters.Pretty, _pretty ? LowerCaseTrue : LowerCaseFalse);
            }
            if (_credentials.Count > 0)
            {
                var creds = new List<dynamic>();
                foreach (var credential in _credentials)
                {
                    creds.Add(new { user = credential.Key, pass = credential.Value });
                }
                formValues.Add(QueryParameters.Creds, EncodeParameter(creds));
            }
            if (!string.IsNullOrEmpty(_clientContextId))
            {
                formValues.Add(QueryParameters.ClientContextId, _clientContextId);
            }
            return formValues;
        }

        /// <summary>
        /// Gets the query parameters.
        /// </summary>
        /// <returns></returns>
        public string GetQueryParameters()
        {
            var sb = new StringBuilder();
            var formValues = GetFormValues();
            foreach (var formValue in GetFormValues())
            {
                sb.AppendFormat(QueryArgPattern, formValue.Key, formValue.Value);
            }
            if (formValues.Count > 0)
            {
                sb.Remove(sb.Length - 1, 1);
            }
            return sb.ToString();
        }

        /// <summary>
        /// JSON encodes the parameter and URI escapes the input parameter.
        /// </summary>
        /// <param name="parameter">The parameter to encode.</param>
        /// <returns>A JSON and URI escaped copy of the parameter.</returns>
        static string EncodeParameter(object parameter)
        {
            return Uri.EscapeDataString(JsonConvert.SerializeObject(parameter));
        }

        /// <summary>
        /// Creates a new <see cref="QueryRequest"/> object.
        /// </summary>
        /// <returns></returns>
        public static IQueryRequest Create()
        {
            return new QueryRequest();
        }

        /// <summary>
        /// Creates a new <see cref="QueryRequest"/> object with the specified statement.
        /// </summary>
        /// <param name="statement">The statement.</param>
        /// <returns></returns>
        public static IQueryRequest Create(string statement)
        {
            return new QueryRequest(statement);
        }

        /// <summary>
        /// Creates the specified plan.
        /// </summary>
        /// <param name="plan">The plan.</param>
        /// <returns></returns>
        public static IQueryRequest Create(IQueryPlan plan)
        {
            return new QueryRequest(plan);
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
                request = GetRequestUri().ToString();
            }
            catch
            {
                request = string.Empty;
            }
            return request;
        }
    }
}
