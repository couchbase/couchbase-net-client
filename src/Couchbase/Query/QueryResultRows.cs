using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Couchbase.Query
{
    internal class QueryResultRows<T> : IAsyncEnumerable<T>
    {
        private readonly StreamingQueryResult<T> _queryResult;
        private readonly JsonTextReader _reader;
        private volatile bool _hasReadResults;

        public QueryResultRows(StreamingQueryResult<T> queryResult, JsonTextReader reader)
        {
            _queryResult = queryResult ?? throw new ArgumentNullException(nameof(queryResult));
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// An enumerator that can be used to iterate through the collection.
        /// </returns>
        public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            if (_hasReadResults)
            {
                // Don't allow enumeration more than once

                throw new StreamAlreadyReadException();
            }

            if (!_queryResult.HasFinishedReading)
            {
                // Read isn't complete, so the stream is currently waiting to deserialize the results

                while (_reader.Read())
                {
                    if (_reader.Depth == 2)
                    {
                        yield return await ReadItem(_reader, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    if (_reader.Path == "results" && _reader.TokenType == JsonToken.EndArray)
                    {
                        break;
                    }
                }

                // Read any remaining attributes after the results
                await _queryResult.ReadResponseAttributes(cancellationToken).ConfigureAwait(false);
            }

            _hasReadResults = true;
        }

        /// <summary>
        /// Reads the object at the current index within the reader.
        /// </summary>
        /// <param name="jtr">The JTR.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns></returns>
        private static async Task<T> ReadItem(JsonTextReader jtr, CancellationToken cancellationToken)
        {
            var jObject = await JToken.ReadFromAsync(jtr, cancellationToken)
                .ConfigureAwait(false);
            return jObject.ToObject<T>();
        }
    }
}
