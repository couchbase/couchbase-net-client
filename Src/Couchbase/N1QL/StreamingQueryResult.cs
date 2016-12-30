using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using Couchbase.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Couchbase.N1QL
{
    /// <summary>
    /// Represents a streaming N1QL response for reading each item as they become available over the network.
    /// Note that unless <see cref="ForceRead"/> is called, there is no underlying collection of representing
    /// the response. If <see cref="ForceRead"/> is called, then the entire response will be read into a temporary
    /// collection. This has the ramification of increasing memory usage and negates the benefits of streaming.
    /// </summary>
    /// <typeparam name="T">A POCO that matches each row of the reponse.</typeparam>
    /// <seealso cref="Couchbase.N1QL.IQueryResult{T}" />
    public class StreamingQueryResult<T> : IQueryResult<T>
    {
        private JsonTextReader _reader;
        private bool? _success;
        private Guid _requestId;
        private string _clientContextId;
        private dynamic _signature;
        private QueryStatus _status;
        private List<Error> _errors = new List<Error>();
        private List<Warning> _warnings = new List<Warning>();
        private Metrics _metrics = new Metrics();
        private volatile bool _hasBeenRead;
        private volatile bool _forcedRead;
        private List<T> _rows;

        /// <summary>
        /// Checks if the stream has been read. Note you cannot call most properties without reading the
        /// stream or calling <see cref="ForceRead"/> first.
        /// </summary>
        /// <exception cref="StreamMustBeReadException"></exception>
        private void CheckRead()
        {
            if (_hasBeenRead) return;
            throw new StreamMustBeReadException(ExceptionUtil.StreamMustBeReadMsg);
        }

        /// <summary>
        /// Gets or sets the query timer.
        /// </summary>
        /// <value>
        /// The query timer.
        /// </value>
        public QueryTimer QueryTimer { get; set; }

        /// <summary>
        /// Returns true if the operation was succesful.
        /// </summary>
        /// <remarks>
        /// If Success is false, use the Message property to help determine the reason.
        /// </remarks>
        public bool Success
        {
            get
            {
                return _success.HasValue && _success.Value;
            }
            internal set { _success = value; }
        }

        /// <summary>
        /// If the operation wasn't succesful, a message indicating why it was not succesful.
        /// </summary>
        public string Message { get; internal set; }

        /// <summary>
        /// If Success is false and an exception has been caught internally, this field will contain the exception.
        /// </summary>
        public Exception Exception { get; internal set; }

        /// <summary>
        /// If the response indicates the request is retryable, returns true.
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// Intended for internal use only.
        /// </remarks>
        public bool ShouldRetry()
        {
            var retry = false;
            switch (Status)
            {
                case QueryStatus.Success:
                case QueryStatus.Errors:
                case QueryStatus.Running:
                case QueryStatus.Completed:
                case QueryStatus.Stopped:
                    break;
                case QueryStatus.Timeout:
                case QueryStatus.Fatal:
                    var status = (int)HttpStatusCode;
                    if (status > 399)
                    {
                        break;
                    }
                    retry = true;
                    break;
            }
            return retry;
        }

        /// <summary>
        /// Gets A unique identifier for the response.
        /// </summary>
        /// <value>
        /// The unique identifier for the response.
        /// </value>
        public Guid RequestId
        {
            get
            {
                CheckRead();
                return _requestId;
            }
            private set { _requestId = value; }
        }

        /// <summary>
        /// Gets the clientContextID of the request, if one was supplied. Used for debugging.
        /// </summary>
        /// <value>
        /// The client context identifier.
        /// </value>
        public string ClientContextId
        {
            get
            {
                CheckRead();
                return _clientContextId;
            }
            private set { _clientContextId = value; }
        }

        /// <summary>
        /// Gets the schema of the results. Present only when the query completes successfully.
        /// </summary>
        /// <value>
        /// The signature of the schema of the request.
        /// </value>
        public dynamic Signature
        {
            get
            {
                CheckRead();
                return _signature;
            }
            private set { _signature = value; }
        }

        /// <summary>
        /// Gets a list of all the objects returned by the query. An object can be any JSON value.
        /// </summary>
        /// <value>
        /// A a list of all the objects returned by the query.
        /// </value>
        public List<T> Rows
        {
            get { return _rows; }
        }

        /// <summary>
        /// Gets the status of the request; possible values are: success, running, errors, completed, stopped, timeout, fatal.
        /// </summary>
        /// <value>
        /// The status of the request.
        /// </value>
        public QueryStatus Status
        {
            get
            {
                CheckRead();
                return _status;
            }
            internal set { _status = value; }
        }

        /// <summary>
        /// Gets a list of 0 or more error objects; if an error occurred during processing of the request, it will be represented by an error object in this list.
        /// </summary>
        /// <value>
        /// The errors.
        /// </value>
        public List<Error> Errors
        {
            get
            {
                CheckRead();
                return _errors;
            }
            private set { _errors = value; }
        }

        /// <summary>
        /// Gets a list of 0 or more warning objects; if a warning occurred during processing of the request, it will be represented by a warning object in this list.
        /// </summary>
        /// <value>
        /// The warnings.
        /// </value>
        public List<Warning> Warnings
        {
            get
            {
                CheckRead();
                return _warnings;
            }
            private set { _warnings = value; }
        }

        /// <summary>
        /// Gets an object containing metrics about the request.
        /// </summary>
        /// <value>
        /// The metrics.
        /// </value>
        public Metrics Metrics
        {
            get
            {
                CheckRead();
                return _metrics;
            }
            private set { _metrics = value; }
        }

        /// <summary>
        /// Gets the HTTP status code.
        /// </summary>
        /// <value>
        /// The HTTP status code.
        /// </value>
        [IgnoreDataMember]
        public HttpStatusCode HttpStatusCode { get; internal set; }

        /// <summary>
        /// Gets or sets the response stream.
        /// </summary>
        /// <value>
        /// The response stream.
        /// </value>
        internal Stream ResponseStream { get; set; }

        /// <summary>
        /// Forces the stream to be read storing the contents in a collection. For performance reasons
        /// calling this is generally considered an anti-pattern.
        /// </summary>
        public void ForceRead()
        {
            _forcedRead = true;
            _rows = this.ToList();
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// An enumerator that can be used to iterate through the collection.
        /// </returns>
        public IEnumerator<T> GetEnumerator()
        {
            if (!_hasBeenRead)
            {
                _reader = new JsonTextReader(new StreamReader(ResponseStream));
            }
            if (_forcedRead && _hasBeenRead)
            {
                foreach (var row in Rows)
                {
                    yield return row;
                }
            }
            _hasBeenRead = true;
            while (_reader.Read())
            {
                if (_reader.Path == "requestID" && _reader.TokenType == JsonToken.String)
                {
                    RequestId = Guid.Parse(_reader.Value.ToString());
                }
                else if (_reader.Path == "status" && _reader.TokenType == JsonToken.String)
                {
                    QueryStatus status;
                    if (Enum.TryParse(_reader.Value.ToString(), true, out status))
                    {
                        Status = status;
                        Success = status == QueryStatus.Success;
                    }
                }
                else if (_reader.Path == "clientContextID" && _reader.TokenType == JsonToken.String)
                {
                    ClientContextId = _reader.Value.ToString();
                }
                else if (_reader.Path == "signature")
                {
                    Signature = JToken.ReadFrom(_reader);
                }
                else if (_reader.Path == "metrics.elapsedTime" && _reader.TokenType == JsonToken.String)
                {
                    _metrics.ElaspedTime = _reader.Value.ToString();
                }
                else if (_reader.Path == "metrics.executionTime" && _reader.TokenType == JsonToken.String)
                {
                    _metrics.ExecutionTime = _reader.Value.ToString();
                }
                else if (_reader.Path == "metrics.resultCount" && _reader.TokenType == JsonToken.Integer)
                {
                    var resultCount = 0u;
                    if (uint.TryParse(_reader.Value.ToString(), out resultCount))
                    {
                        _metrics.ResultCount = resultCount;
                    }
                }
                else if (_reader.Path == "metrics.resultSize" && _reader.TokenType == JsonToken.Integer)
                {
                    var resultSize = 0u;
                    if (uint.TryParse(_reader.Value.ToString(), out resultSize))
                    {
                        _metrics.ResultSize = resultSize;
                    }
                }
                else if (_reader.Path == "results")
                {
                    while (_reader.Read())
                    {
                        if (_reader.Depth == 2 && _reader.TokenType == JsonToken.StartObject)
                        {
                            yield return ReadObject<T>(_reader);
                        }
                        if (_reader.Path == "results" && _reader.TokenType == JsonToken.EndArray)
                        {
                            break;
                        }
                    }
                }
                else if(_reader.Path == "warnings")
                {
                    while (_reader.Read())
                    {
                        if (_reader.Depth == 2 && _reader.TokenType == JsonToken.StartObject)
                        {
                           Warnings.Add(ReadObject<Warning>(_reader));
                        }
                        if (_reader.Path == "warnings" && _reader.TokenType == JsonToken.EndArray)
                        {
                            break;
                        }
                    }
                }
                else if (_reader.Path == "errors")
                {
                    while (_reader.Read())
                    {
                        if (_reader.Depth == 2 && _reader.TokenType == JsonToken.StartObject)
                        {
                            Errors.Add(ReadObject<Error>(_reader));
                        }
                        if (_reader.Path == "errors" && _reader.TokenType == JsonToken.EndArray)
                        {
                            break;
                        }
                    }
                }
            }

            if (QueryTimer != null)
            {
                QueryTimer.ClusterElapsedTime = Metrics.ElaspedTime;
            }
        }

        /// <summary>
        /// Reads the object at the current index within the reader.
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <param name="jtr">The JTR.</param>
        /// <returns></returns>
        private K ReadObject<K>(JsonTextReader jtr)
        {
            if (jtr.TokenType == JsonToken.StartObject ||
                jtr.TokenType == JsonToken.StartArray ||
                jtr.TokenType == JsonToken.StartConstructor)
            {
                var jObject = JToken.ReadFrom(jtr);
                return jObject.ToObject<K>();
            }
            return default(K);
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (_reader != null)
            {
                _reader.Close(); // also closes underlying stream
                _reader = null;
            }
            if (QueryTimer != null)
            {
                QueryTimer.Dispose();
                QueryTimer = null;
            }
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
