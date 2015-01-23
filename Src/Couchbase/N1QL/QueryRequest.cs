using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;

namespace Couchbase.N1QL
{
    public class QueryRequest : IQueryRequest, IPreparable
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

        public static readonly ConcurrentDictionary<string, string> PreparedStatementCache = new ConcurrentDictionary<string, string>();

        public static readonly Dictionary<ScanConsistency, string> ScanConsistencyResolver = new Dictionary<ScanConsistency, string>
        {
            {N1QL.ScanConsistency.AtPlus, "at_plus"},
            {N1QL.ScanConsistency.NotBounded, "not_bounded"},
            {N1QL.ScanConsistency.RequestPlus, "request_plus"},
            {N1QL.ScanConsistency.StatementPlus, "statement_plus"}
        };

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

        /// <summary>
        /// Returns true if the request is a prepared statement
        /// </summary>
        public bool IsPrepared { get { return _prepared; } }

        public IQueryRequest Prepared(bool prepared)
        {
            _prepared = prepared;
            return this;
        }

        public IQueryRequest HttpMethod(Method method)
        {
            _method = method;
            return this;
        }

        public IQueryRequest Statement(string statement)
        {
            if (string.IsNullOrWhiteSpace(statement))
            {
                throw new ArgumentNullException("statement");
            }
            _statement = statement;
            return this;
        }

        public IQueryRequest Timeout(TimeSpan timeOut)
        {
            _timeOut = timeOut;
            return this;
        }

        public IQueryRequest ReadOnly(bool readOnly)
        {
            _readOnly = readOnly;
            return this;
        }

        public IQueryRequest Metrics(bool includeMetrics)
        {
            _includeMetrics = includeMetrics;
            return this;
        }

        public IQueryRequest AddNamedParameter(string name, object value)
        {
            _parameters.Add(name, value);
            return this;
        }

        public IQueryRequest AddPositionalParameter(object value)
        {
            _arguments.Add(value);
            return this;
        }

        public IQueryRequest AddNamedParameter(params KeyValuePair<string, object>[] parameters)
        {
            foreach (var parameter in parameters)
            {
                _parameters.Add(parameter.Key, parameter.Value);
            }
            return this;
        }

        public IQueryRequest AddPositionalParameter(params object[] parameters)
        {
            foreach (var parameter in parameters)
            {
                _arguments.Add(parameter);
            }
            return this;
        }

        public IQueryRequest Format(Format format)
        {
            _format = format;
            return this;
        }

        public IQueryRequest Encoding(Encoding encoding)
        {
            _encoding = encoding;
            return this;
        }

        public IQueryRequest Compression(Compression compression)
        {
            _compression = compression;
            return this;
        }

        public IQueryRequest Signature(bool includeSignature)
        {
            _includeSignature = includeSignature;
            return this;
        }

        public IQueryRequest ScanConsistency(ScanConsistency scanConsistency)
        {
            _scanConsistency = scanConsistency;
            return this;
        }

        public IQueryRequest ScanVector(dynamic scanVector)
        {
            _scanVector = scanVector;
            return this;
        }

        public IQueryRequest ScanWait(TimeSpan scanWait)
        {
            _scanWait = scanWait;
            return this;
        }

        public IQueryRequest Pretty(bool pretty)
        {
            _pretty = pretty;
            return this;
        }

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

        public IQueryRequest ClientContextId(string clientContextId)
        {
            _clientContextId = clientContextId;
            return this;
        }

        public IQueryRequest BaseUri(Uri baseUri)
        {
            _baseUri = baseUri;
            return this;
        }

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

        public Uri GetRequestUri()
        {
            if (string.IsNullOrWhiteSpace(_statement))
            {
                throw new ArgumentException("A statement must be provided.");
            }
            CheckMethod();

            //build the request query starting with the base uri- e.g. http://localhost:8093/query
            var sb = new StringBuilder();
            sb.Append(_baseUri + "?");

            string statement;
            var prepared = GetStatement(out statement);
            sb.AppendFormat(QueryArgPattern, prepared ? QueryParameters.Prepared : QueryParameters.Statement, statement);
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
                sb.AppendFormat(QueryArgPattern, QueryParameters.ScanConsistency, _scanConsistency);
            }
            if (_scanVector != null)
            {
                sb.AppendFormat(QueryArgPattern, QueryParameters.ScanVector, _scanVector);
            }
            if (_scanWait.HasValue)
            {
                sb.AppendFormat(QueryArgPattern, QueryParameters.ScanWait, _scanWait);
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

        public IDictionary<string, string> GetFormValues()
        {
            if (string.IsNullOrWhiteSpace(_statement))
            {
                throw new ArgumentException("A statement must be provided.");
            }
            CheckMethod();

            //build the request query starting with the base uri- e.g. http://localhost:8093/query
            IDictionary<string, string> formValues = new Dictionary<string, string>();

            string statement;
            var prepared = GetStatement(out statement);
            formValues.Add(prepared ? QueryParameters.Prepared : QueryParameters.Statement, statement);

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
                formValues.Add(QueryParameters.Args, JsonConvert.SerializeObject(_arguments));
            }
            if (_format.HasValue)
            {
                formValues.Add(QueryParameters.Format, EncodeParameter(_format));
            }
            if (_encoding.HasValue)
            {
                formValues.Add(QueryParameters.Encoding, EncodeParameter(_encoding));
            }
            if (_compression.HasValue)
            {
                formValues.Add(QueryParameters.Compression, EncodeParameter(_compression.ToString()));
            }
            if (_includeSignature.HasValue)
            {
                formValues.Add(QueryParameters.Signature, _includeSignature.Value ? LowerCaseTrue : LowerCaseFalse);
            }
            if (_scanConsistency.HasValue)
            {
                formValues.Add(QueryParameters.ScanConsistency, EncodeParameter(ScanConsistencyResolver[_scanConsistency.Value]));
            }
            if (_scanVector != null)
            {
                formValues.Add(QueryParameters.ScanVector, _scanVector);
            }
            if (_scanWait.HasValue)
            {
                formValues.Add(QueryParameters.ScanWait, EncodeParameter((uint)_scanWait.Value.TotalMilliseconds));
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

        public string GetQueryParameters()
        {
            var sb = new StringBuilder();
            foreach (var formValue in GetFormValues())
            {
                sb.AppendFormat(QueryArgPattern, formValue.Key, formValue.Value);
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

        public static IQueryRequest Create()
        {
            return new QueryRequest();
        }

        public static IQueryRequest Create(string statement, bool isPrepared)
        {
            return new QueryRequest().Statement(statement).Prepared(isPrepared);
        }

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

        /// <summary>
        /// Gets the statement to send to the server which could be a prepared statement, a request for a prepared statement or the statement itself.
        /// </summary>
        /// <returns></returns>
        bool GetStatement(out string statement)
        {
            var prepared = false;
            if (_prepared && HasPrepared)
            {
                if (!PreparedStatementCache.TryGetValue(_statement, out statement))
                {
                    //Something went wrong use the regular statement
                    statement = _statement;
                }
                else
                {
                    prepared = true;
                }
            }
            else
            {
                if (_prepared && !_statement.Contains("PREPARE "))
                {
                    statement = string.Concat("PREPARE ", _statement);
                }
                else
                {
                    statement = _statement;
                }
            }
            return prepared;
        }

        /// <summary>
        /// Returns true if the statement exists and an entry exists for it's prepared statment in the internal cache.
        /// </summary>
        public bool HasPrepared
        {
            get
            {
                return !string.IsNullOrWhiteSpace(_statement) &&
                    PreparedStatementCache.ContainsKey(_statement);
            }
        }

        /// <summary>
        /// Caches a prepared statement locally updating it if a key exists for the statement.
        /// </summary>
        /// <param name="preparedStatement">The prepared statement to cache.</param>
        public void CachePreparedStatement(string preparedStatement)
        {
            if (string.IsNullOrWhiteSpace(_statement))
            {
                throw new ArgumentException("A statement must be provided.");
            }
            if (string.IsNullOrWhiteSpace(preparedStatement))
            {
                throw new ArgumentException("A prepared statement must be provided.");
            }
            PreparedStatementCache.AddOrUpdate(_statement, preparedStatement, (key, oldvalue) => preparedStatement);
        }

        /// <summary>
        /// Clears all cached prepared statements
        /// </summary>
        public void ClearCache()
        {
            PreparedStatementCache.Clear();
        }
    }
}
