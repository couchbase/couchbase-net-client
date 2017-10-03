using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Couchbase.Views
{
    /// <summary>
    /// Represents a streaming View response for reading each row as it becomes available over the network.
    /// Note that unless there is no underlying collection representing the response, instead the rows are extracted
    /// from the stream one at a time. If the Enumeration is evaluated, eg calling ToList(), then the entire response
    /// will be read. Once a row has been read from the stream, it is not available to be read again.
    /// A <see cref="StreamAlreadyReadException"/> will be thrown if the result is enumerated after it has reached
    /// the end of the stream.
    /// </summary>
    /// <typeparam name="T">A POCO that matches each row of the reponse.</typeparam>
    /// <seealso cref="IViewResult{T}" />
    public class StreamingViewResult<T> : ViewResult<T>
    {
        private readonly Result _result;
        public static JsonSerializer _jsonSerializer = new JsonSerializer();

        public StreamingViewResult()
        { }

        public StreamingViewResult(bool success, HttpStatusCode statusCode, string message, Stream responseStream)
        {
            Success = success;
            StatusCode = statusCode;
            Message = message;

            _jsonSerializer.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            _result = new Result(responseStream);
        }

        /// <summary>
        /// The total number of rows.
        /// </summary>
        public override uint TotalRows
        {
            get { return _result.TotalRows; }
        }

        /// <summary>
        /// The results of the query if successful as a <see cref="IEnumerable{T}"/>
        /// </summary>
        public override IEnumerable<ViewRow<T>> Rows
        {
            get { return _result; }
        }

        /// <summary>
        /// Returns the value of each element within the <see cref="Rows"/> property as a <see cref="IEnumerable{T}"/>.
        /// </summary>
        public override IEnumerable<T> Values
        {
            get { return _result.Select(row => row.Value); }
        }

        private class Result : IEnumerable<ViewRow<T>>, IDisposable
        {
            private readonly Stream _responseStream;
            private JsonTextReader _reader;
            private volatile bool _hasFinishedReading;
            private uint _totalRows;

            public Result(Stream responseStream)
            {
                _responseStream = responseStream;
            }

            public uint TotalRows
            {
                get
                {
                    ReadToRows();
                    return _totalRows;
                }
            }

            public IEnumerator<ViewRow<T>> GetEnumerator()
            {
                if (_hasFinishedReading)
                {
                    throw new StreamAlreadyReadException();
                }

                // make sure we're are the 'rows' element in the stream
                if (ReadToRows())
                {
                    while (_reader.Read())
                    {
                        if (_reader.TokenType == JsonToken.StartObject)
                        {
                            yield return JToken.ReadFrom(_reader).ToObject<ViewRow<T>>(_jsonSerializer);
                        }
                    }
                }

                // we've reached the end of the stream, so mark it as finished reading
                _hasFinishedReading = true;
            }

            private bool ReadToRows()
            {
                // if reader is not null, we've already started to read the stream
                if (_reader != null)
                {
                    return true;
                }

                _reader = new JsonTextReader(new StreamReader(_responseStream));
                while (_reader.Read())
                {
                    if (_reader.Path == "total_rows" && _reader.TokenType == JsonToken.Integer)
                    {
                        uint totalRows;
                        if (uint.TryParse(_reader.Value.ToString(), out totalRows))
                        {
                            _totalRows = totalRows;
                        }
                    }
                    else if (_reader.Path == "rows" && _reader.TokenType == JsonToken.StartArray)
                    {
                        // we've reached the 'rows' element, return now so when we enumerate we start from here
                        return true;
                    }
                }

                // if we got here, there was no 'rows' element in the stream
                _hasFinishedReading = true;
                return false;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public void Dispose()
            {
                if (_reader != null)
                {
                    _reader.Close(); // also closes underlying stream
                    _reader = null;
                }
            }
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
